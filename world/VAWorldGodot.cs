namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public bool Initialised => world != null;

    public override void _EnterTree()
    {
        SetNotifyTransform(true);

        // A VAWorld only has a position - if it was rotated/scaled while parented to another node
        // and then unparented, those bake into its own transform. Reset them here so the debug
        // window and primitive baking aren't thrown off by an inherited rotation.
        NormalizeTransform();

        if (Engine.IsEditorHint())
            return;

        // Ensure the OpenAL device/context exists before creating reverb effects below
        ALManager.Ensure();

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

        // Re-run the setter now that world exists - it also drops RenderingEnabled on macOS (see VAWorldProperties).
        RenderingEnabled = _RenderingEnabled;
        

        world.AirAbsorption.Humidity = Humidity;
        world.AirAbsorption.Temperature = Temperature;
        world.AirAbsorption.Pressure = Pressure;

        // Create reverb effects
        OnDeviceRecreated();

        if (!ALManager.Initialised)
        {
            LogError("The godot-mono-openal addon is not enabled. Ensure godot-mono-openal is enabled in Project Settings > Plugins (try toggling it off and on if it's already enabled)");
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

        NormalizeTransform();

        // Rebuild the bounds gizmo whenever the node moves
        UpdateGizmos();

        if (world != null)
            world.Position = ToVAudio(Position);
    }

    void NormalizeTransform()
    {
        if (Quaternion != Quaternion.Identity)
            Quaternion = Quaternion.Identity;

        if (Scale != Vector3.One)
            Scale = Vector3.One;
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
        // SceneRoot can be null if this node isn't under CurrentScene (e.g. added as a sibling autoload) -
        // scan from the tree root instead so primitives already baked into the scene aren't missed.
        Node root = GetTree()?.Root;

        if (root == null)
            return;

        foreach (var child in root.GetChildren())
            AddPrimitive(child, vaudio.MaterialType.Air, true);

        // Listen for scene tree changes
        GetTree().NodeAdded += OnNodeAdded;
        GetTree().NodeRemoved += OnNodeRemoved;
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint())
            return;

        ALManager.UnregisterDeviceDestroyedCallback(OnDeviceDestroyed);
        ALManager.UnregisterDeviceRecreatedCallback(OnDeviceRecreated);

        GetTree().NodeAdded -= OnNodeAdded;
        GetTree().NodeRemoved -= OnNodeRemoved;

        UnregisterDebuggerCapture();

        if (SceneRoot != null)
            RemovePrimitive(SceneRoot, true);

        world?.Dispose();
    }

    // This fires for the new parent node AND each of its child nodes separately
    //  Parent node is invoked first
    void OnNodeAdded(Node node) => AddPrimitive(node, vaudio.MaterialType.Air, false);

    // This fires for the new parent node AND each of its child nodes separately
    //  Child nodes are invoked first
    void OnNodeRemoved(Node node) => RemovePrimitive(node, false);

    internal bool NoListenerWarningLogged;

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
        {
            if (SyncViewport)
                SendViewportCameraToRunningGame();

            return;
        }

        if (listener == null)
        {
            if (!NoListenerWarningLogged)
            {
                LogWarning($"Node {Name} has no main listener, so reverb cannot be updated. Add a VAListener node to this scene");
                NoListenerWarningLogged = true;
            }
        }
        else if (ALManager.Initialised)
        {
            // Sync the AL listener to our main listener
            Vector3 listenerRotation = listener.GlobalRotation;

            ALManager.ListenerPosition = listener.GlobalPosition;
            ALManager.ListenerPitch = listenerRotation.X;
            ALManager.ListenerYaw = listenerRotation.Y;
        }

        ALManager.Update();
        world.Update();
    }

}
