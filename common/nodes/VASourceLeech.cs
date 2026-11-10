namespace vaudio_godot_mono_openal;

public partial class VASourceLeech
{
    private VAWorld vercidiumAudio;
    private VAEmitter emitter;

    public bool Raytraced => emitter != null && emitter.Raytraced;

    /// <summary>
    /// See <see cref="VAEmitter.IsReadyToPlay"/>
    /// </summary>
    public bool IsReadyToPlay => emitter != null && emitter.IsReadyToPlay;

    /// <summary>
    /// The parent emitter's <see cref="VAEmitter.IsSilencedByBounds"/>, using its <see cref="VAEmitter.ClampPosition"/>
    /// </summary>
    public bool IsSilencedByBounds() => emitter != null && emitter.IsSilencedByBounds();

    readonly VABoundsPlayback playback = new();

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

    // Autoplay waits for the parent emitter to be ready to play and inside the bounds
    public override void _Ready()
    {
    }

    // Matches VASource's Play() override: returns false without playing until the parent emitter is ready to play, and follows the same bounds rules
    public override bool Play()
    {
        if (playback.ApplyOnPlay(this, IsSilencedByBounds()))
            return false;

        // Wait for the parent emitter's muffling and reverb results, so the sound never starts unmuffled or without reverb
        if (!IsReadyToPlay)
            return false;

        var played = base.Play();

        if (played)
            playback.OnPlayed();

        return played;
    }

    // Also cancels a play that's waiting, e.g. a loop waiting outside the bounds
    public override void Stop()
    {
        playback.Cancel();
        base.Stop();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Engine.IsEditorHint())
            return;

        var silenced = playback.Apply(this, IsSilencedByBounds());

        if (!Raytraced)
            return;

        if (!silenced && playback.IsPending(Autoplay) && IsReadyToPlay)
            Play();

        if (vercidiumAudio?.listener == null)
            return;

        ApplyRaytracingResults(vercidiumAudio.listener.emitter);
    }

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
        _wasPlayingBeforeDeviceDestroyed = playback.AutoplayDone && Looping;

        // Reset played state since sources are being destroyed
        playback.AutoplayDone = false;

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
