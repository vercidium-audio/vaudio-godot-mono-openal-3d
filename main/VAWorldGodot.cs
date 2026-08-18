using godot_openal;

namespace vaudio_godot_openal;

public partial class VAWorld : Node3D
{
    public bool Initialised => world != null;

    public override void _EnterTree()
    {
        // Node3D.Position/Transform can't be overridden or shadowed in a way the engine/editor
        // will actually call (Inspector edits and gizmo drags go straight through the engine's
        // native set_position, bypassing any C# property) - NOTIFICATION_TRANSFORM_CHANGED is
        // the only hook that fires uniformly for all three, so opt into it here.
        SetNotifyTransform(true);

        if (Engine.IsEditorHint())
            return;

        // Cache the scene root since we access it often
        SceneRoot = GetTree().CurrentScene as Node3D;

        world = new();
        
        world.LogCallback = Log;
        world.Position = ToVAudio(Position);
        world.Size = ToVAudio(Size);
        world.Epsilon = Epsilon;
        world.WorldIsIndoors = WorldIsIndoors;

        // Reverb
        world.MaximumGroupedEAXCount = MaximumGroupedEAXCount;
        world.OnReverbUpdated = OnReverbUpdated;

        // Air absorption
        world.MetersPerUnit = MetersPerUnit;
        world.InverseSpeedOfSound = 1.0f / SpeedOfSound;
        world.ReferenceFrequencyLF = ReferenceFrequencyLF;
        world.ReferenceFrequencyHF = ReferenceFrequencyHF;

        // Emitters 
        world.EmittersOutsideTheWorldAreMuffled = EmittersOutsideTheWorldAreMuffled;

        // Threading
        // 0 maps to processor count - 1, matching the native plugin's behaviour
        world.MaximumConcurrencyLevel = MaximumConcurrencyLevel == 0 ? vaudio.ThreadStatistics.BackgroundThreadCount : MaximumConcurrencyLevel;
        world.WorkItemCount = WorkItemCount;

        world.RenderingEnabled = RenderingEnabled;
        

        world.AirAbsorption.Humidity = Humidity;
        world.AirAbsorption.Temperature = Temperature;
        world.AirAbsorption.Pressure = Pressure;

        // Create reverb effects
        OnDeviceRecreated();

        if (!GodotOpenALEnabled)
        {
            LogError("The godot-openal addon is not enabled. Ensure godot-openal is enabled (try toggling it off and on), and ensure the ALManager autoload is enabled in Project Settings > Globals");
        }

        // Register for device destroyed/recreated callbacks to clean up and recreate reverb effects
        RegisterDeviceRecreatedCallback(OnDeviceRecreated);
        RegisterDeviceDestroyedCallback(OnDeviceDestroyed);

        // Wait a frame for the scene to be fully loaded
        CallDeferred(nameof(InitializeScene));

        RegisterDebuggerCapture();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationTransformChanged)
            return;

        // The bounds AABB is always axis-aligned (see VAWorldProperties._ValidateProperty, which
        // hides rotation/scale in the Inspector) - but the viewport's Rotate tool bypasses the
        // Inspector and can still rotate the node directly, so snap it back out here too.
        if (Quaternion != Quaternion.Identity)
            Quaternion = Quaternion.Identity;

        // Rebuild the bounds gizmo whenever the node moves, whether from the viewport gizmo,
        // the Inspector's Position field, or code.
        UpdateGizmos();

        if (world != null)
            world.Position = ToVAudio(Position);
    }

    void OnDeviceRecreated()
    {
        // Recreate the reverb effects after the device is recreated
        listenerReverbEffect = new();
    }

    void OnDeviceDestroyed()
    {
        // Delete all reverb effects - they contain OpenAL resources that are now invalid
        ambientFilter?.Delete();
        ambientFilter = null;

        listenerReverbEffect?.Dispose();
        listenerReverbEffect = null;

        foreach (var effect in groupedReverbEffects)
            effect.Dispose();

        groupedReverbEffects.Clear();
    }

    void InitializeScene()
    {
        foreach (var child in SceneRoot.GetChildren())
            AddPrimitive(child, vaudio.MaterialType.Air, true);

        // Listen for scene tree changes
        GetTree().NodeAdded += OnNodeAdded;
        GetTree().NodeRemoved += OnNodeRemoved;
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint())
            return;

        // instance may be null if ALManager was never initialised
        ALManager.instance?.UnregisterDeviceDestroyedCallback(OnDeviceDestroyed);
        ALManager.instance?.UnregisterDeviceRecreatedCallback(OnDeviceRecreated);

        GetTree().NodeAdded -= OnNodeAdded;
        GetTree().NodeRemoved -= OnNodeRemoved;

        UnregisterDebuggerCapture();

        // Remove vercidium_audio_* metadata fields from all nodes in the scene
        RemovePrimitive(SceneRoot, true);

        world?.Dispose();
    }

    // This fires for the new parent node AND each of its child nodes separately
    //  Parent node is invoked first
    void OnNodeAdded(Node node) => AddPrimitive(node, vaudio.MaterialType.Air, false);

    // This fires for the new parent node AND each of its child nodes separately
    //  Child nodes are invoked first
    void OnNodeRemoved(Node node) => RemovePrimitive(node, false);

    bool NoListenerErrorLogged;

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
            return;

        if (listener == null)
        {
            if (!NoListenerErrorLogged)
            {
                LogError($"Failed to update node {Name} because there is no main listener. Ensure a VAEmitter exists with `IsMainListener` set to true");
                NoListenerErrorLogged = true;
            }

            return;
        }

        // Sync the AL listener to our main listener
        Vector3 listenerRotation = listener.GlobalRotation;

        if (GodotOpenALEnabled)
        {
            ALManager.instance.ListenerPosition = listener.GlobalPosition;
            ALManager.instance.ListenerPitch = listenerRotation.X;
            ALManager.instance.ListenerYaw = listenerRotation.Y;
        }

        // Render the debug window from the perspective of the main listener
        world.CameraPosition = ToVAudio(listener.GlobalPosition);
        world.CameraPitch = listenerRotation.X;
        world.CameraYaw = listenerRotation.Y;
        world.FieldOfView = float.DegreesToRadians(90);

        world.Update();
    }

}
