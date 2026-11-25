namespace vaudio_godot_mono_openal;

// Shared body of VASourceAmbient. Godot binds the script to nodes/VASourceAmbient.cs (the file named after the class), which must stay at that path so scenes saved by older versions still load
public partial class VASourceAmbient
{
    private VAWorld vercidiumAudio;
    private bool played = false;
    private bool warnedNoAmbience = false;
    private bool warnedNoListenerReverb = false;

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
    }

    public override void _ExitTree()
    {
        cancelWaitForVAWorld?.Invoke();
        cancelWaitForVAWorld = null;

        if (!Engine.IsEditorHint())
            VAAudioBackend.UnregisterDeviceRecreatedCallback(OnDeviceRecreated);

        base._ExitTree();
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
            return;

        // Register for a callback to re-play sounds when changing devices
        RegisterDeviceRecreatedCallback(OnDeviceRecreated);

        base._Ready();
    }

    protected override void ConfigureVoice(VAVoice voice) => ConfigureListenerRelativeVoice(voice);

    /// <summary>If true, this source is sent to the listener's reverb bus, muffled by the ambient filter. It then waits for the listener's first raytraced reverb before it plays</summary>
    [Export]
    public bool UseListenerReverb { get; set; } = false;

    public override bool Play(float startTime)
    {
        // Don't play until we've raytraced once, and the listener's reverb has its first EAX, so it never starts in the backend's default reverb
        if (vercidiumAudio?.ambientFilter == null || (UseListenerReverb && !vercidiumAudio.IsListenerReverbReady()))
            return false;

        if (UseListenerReverb)
            vercidiumAudio.WarnIfListenerCastsNoReverbRays(this, ref warnedNoListenerReverb);

        played = base.Play(startTime);
        return played;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Engine.IsEditorHint())
            return;

        var listener = vercidiumAudio?.listener;

        if (!warnedNoAmbience && listener != null && listener.Raytraced && !listener.AmbientOcclusionEnabled && !listener.AmbientPermeationEnabled)
        {
            Logger.LogWarning($"{Name}: The listener '{listener.Name}' does not have ambience enabled, so this VASourceAmbient will never play. Set the listener's ambient occlusion / permeation ray and bounce counts above 0.");
            warnedNoAmbience = true;
        }

        // Ensure VAWorld is available
        if (vercidiumAudio?.ambientFilter is not VALowPass ambientFilter)
            return;

        reverbBus = UseListenerReverb ? vercidiumAudio.listenerReverbBus : null;
        UpdateFilter(ambientFilter.GainLF, ambientFilter.GainHF);

        if (!played)
        {
            played = Play();
        }
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

    bool _wasPlayingBeforeDeviceDestroyed;

    public override void OnDeviceDestroyed()
    {
        // Track if we were playing so we can re-play after device recreation
        _wasPlayingBeforeDeviceDestroyed = played && Looping;

        // Reset played state since sources are being destroyed
        played = false;

        base.OnDeviceDestroyed();
    }
}
