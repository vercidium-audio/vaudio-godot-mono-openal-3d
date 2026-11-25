namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public bool Initialised => world != null;

    public override void _EnterTree()
    {
        SetNotifyTransform(true);

        // A VAWorld can only have a position, however it may inherit rotation/scale when being unparented from another node.
        // Reset rotation/scale here so the editor and debug window always match
        NormalizeTransform();
        ReclampBoundsSize();

        if (Engine.IsEditorHint())
            return;

        // Ensure the audio device exists before creating reverb buses below
        VAAudioBackend.Ensure();
        ApplyBackendSettings();

        // Cache the scene root since we access it often
        SceneRoot = GetTree().CurrentScene as Node3D;

        world = new();
        world.CoordinateSystem = vaudio.CoordinateSystem.Godot;

        world.Name = ResolvedWorldName;
        world.LogCallback = LogSDK;
        boundsPosition = Position;
        PlaceBounds();
        world.Size = ToVAudio(Size);
        world.Epsilon = Epsilon;

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
        world.OcclusionRaysLoseEnergyFromWorldBounds = OcclusionRaysLoseEnergyFromWorldBounds;

        // Threading
        // 0 maps to processor count - 1, matching the native plugin's behaviour
        world.MaximumConcurrencyLevel = MaximumConcurrencyLevel == 0 ? vaudio.ThreadStatistics.BackgroundThreadCount : MaximumConcurrencyLevel;
        world.WorkItemCount = WorkItemCount;

        // Re-run the setter now that the world exists. Handles gl_compatibility and Mac restrictions
        RenderingEnabled = _RenderingEnabled;
        world.VisualisationScale = _VisualisationScale;

        world.AirAbsorption.Humidity = Humidity;
        world.AirAbsorption.Temperature = Temperature;
        world.AirAbsorption.Pressure = Pressure;

        // Create reverb buses
        OnDeviceRecreated();

        // Register for device destroyed/recreated callbacks to clean up and recreate reverb buses
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

        if (world != null && _BoundsFollow != VABoundsFollow.Listener)
            PlaceBounds();
    }

    void NormalizeTransform()
    {
        if (Quaternion != Quaternion.Identity)
            Quaternion = Quaternion.Identity;

        if (Scale != Vector3.One)
            Scale = Vector3.One;
    }

    bool listeningToTree;

    void InitializeScene()
    {
        // The scene can change again before this deferred call runs
        if (!IsInsideTree())
            return;

        // SceneRoot can be null if this node isn't under CurrentScene (e.g. added as a sibling autoload) -
        // scan from the tree root instead so primitives already baked into the scene aren't missed.
        Node root = GetTree().Root;

        foreach (var child in root.GetChildren())
            AddPrimitive(child, vaudio.MaterialType.Air, true);

        // Listen for scene tree changes
        GetTree().NodeAdded += OnNodeAdded;
        GetTree().NodeRemoved += OnNodeRemoved;
        listeningToTree = true;
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint())
            return;

        VAAudioBackend.UnregisterDeviceDestroyedCallback(OnDeviceDestroyed);
        VAAudioBackend.UnregisterDeviceRecreatedCallback(OnDeviceRecreated);

        if (listeningToTree)
        {
            GetTree().NodeAdded -= OnNodeAdded;
            GetTree().NodeRemoved -= OnNodeRemoved;
            listeningToTree = false;
        }

        UnregisterDebuggerCapture();

        if (SceneRoot != null)
            RemovePrimitive(SceneRoot, true);

        world?.Dispose();
    }

    // This fires for the new parent node AND each of its child nodes separately
    //  Parent node is invoked first
    void OnNodeAdded(Node node)
    {
        ResolveInherited(node, out var material, out var useFlatTransmission, out var filter);
        AddPrimitive(node, material, useFlatTransmission, filter, false);
    }

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

        VAAudioBackend.Ensure();
        
        if (listener == null)
        {
            if (!NoListenerWarningLogged)
            {
                LogWarning($"Node {Name} has no main listener, so reverb cannot be updated. Add a VAListener node to this scene");
                NoListenerWarningLogged = true;
            }
        }
        else
        {
            // Sync the backend listener to our main listener
            Vector3 listenerRotation = listener.GlobalRotation;

            VAAudioBackend.ListenerPosition = listener.GlobalPosition;
            VAAudioBackend.ListenerPitch = listenerRotation.X;
            VAAudioBackend.ListenerYaw = listenerRotation.Y;
        }

        FollowBounds();

        VAAudioBackend.Update();
        world.Update();
    }

}
