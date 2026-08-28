namespace vaudio_godot_mono_openal;

// A sound source that leeches raytracing results off a parent VAEmitter instead of
// owning/casting its own rays - useful for sounds that fire frequently from the same
// location (e.g. footsteps, gunshots) without each needing its own raytraced emitter.
// Must be a direct child of a VAEmitter (or VASource, whose child emitter is named
// "{Name}-Emitter") node.
public partial class VASourceLeech
{
    private VAWorld vercidiumAudio;
    private VAEmitter emitter;

    public bool Raytraced => emitter != null && emitter.Raytraced;

    private bool _PlayWhenRaytracingCompletes = true;

    [Export]
    public bool PlayWhenRaytracingCompletes
    {
        get => _PlayWhenRaytracingCompletes;
        set => _PlayWhenRaytracingCompletes = value;
    }

    bool played = false;

    Action cancelWaitForVAWorld;

    public override void _EnterTree()
    {
        base._EnterTree();

        if (Engine.IsEditorHint())
            return;

        cancelWaitForVAWorld = this.WaitForVAWorld(world =>
        {
            cancelWaitForVAWorld = null;
            vercidiumAudio = world;
        });

        emitter = GetParent() as VAEmitter;

        if (emitter == null)
        {
            LogWarning($"'{Name}' is a VASourceLeech but its parent is not a VAEmitter - it will never play. VASourceLeech must be a direct child of a VAEmitter (or VASource) node whose own raytracing results it leeches off.");
            return;
        }

        // Register for a callback to re-play sounds when changing devices
        RegisterDeviceRecreatedCallback(OnDeviceRecreated);
    }

    void OnDeviceRecreated()
    {
        // Re-play if we were playing before the device was destroyed
        if (_wasPlayingBeforeDeviceDestroyed)
        {
            _wasPlayingBeforeDeviceDestroyed = false;
            Play();
        }
    }

    public override bool Play()
    {
        if (!Raytraced)
        {
            PlayWhenRaytracingCompletes = true;
            return false;
        }

        return played = base.Play();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Engine.IsEditorHint())
            return;

        if (!Raytraced)
            return;

        if (!played && PlayWhenRaytracingCompletes)
            Play();

        if (vercidiumAudio?.listener == null)
            return;

        ApplyRaytracingResults(vercidiumAudio.listener.emitter);
    }

    // Same as VASource.ApplyRaytracingResults, but reads results off the parent
    // VAEmitter this node leeches instead of an owned child emitter.
    void ApplyRaytracingResults(vaudio.Emitter other)
    {
        effect = vercidiumAudio.GetReverbEffect(emitter);

        if (other.HasRaytracedTarget(emitter.emitter))
        {
            var vaudioFilter = other.GetTargetFilter(emitter.emitter);
            UpdateFilter(vaudioFilter.GainLF, vaudioFilter.GainHF, true);
        }
    }

    bool _wasPlayingBeforeDeviceDestroyed = false;

    public override void OnDeviceDestroyed()
    {
        // Track if we were playing so we can re-play after device recreation
        _wasPlayingBeforeDeviceDestroyed = played && Looping;

        // Reset played state since sources are being destroyed
        played = false;

        base.OnDeviceDestroyed();
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint())
        {
            base._ExitTree();
            return;
        }

        // Unregister the device recreated callback (only registered when not in editor)
        ALManager.UnregisterDeviceRecreatedCallback(OnDeviceRecreated);

        cancelWaitForVAWorld?.Invoke();
        cancelWaitForVAWorld = null;

        emitter = null;

        base._ExitTree();
    }
}
