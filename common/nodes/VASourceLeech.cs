namespace vaudio_godot_mono_openal;

public partial class VASourceLeech
{
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

    /// <summary>
    /// Current muffling filter state (direct/dry path only). 0 until the listener has raytraced the parent emitter at least once
    /// </summary>
    public float GainLF => filter?.gain ?? 0;
    public float GainHF => filter?.gainHF ?? 0;

    readonly VABoundsPlayback playback = new();

    public override void _EnterTree()
    {
        base._EnterTree();

        if (Engine.IsEditorHint())
            return;

        emitter = GetParent() as VAEmitter;

        if (emitter == null)
        {
            LogWarning($"{Name}: This source will never play. VASourceLeech nodes must be a direct child of a VAEmitter node.");
            return;
        }

        if (emitter.IsMainListener)
        {
            emitter = null;
            LogWarning($"{Name}: This source will never play. VASourceLeech nodes can't be a child of a VAListener, use a VASourceRelative instead.");
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

        ApplyRaytracingResults();
    }

    // Copies the parent VAEmitter's muffling and reverb, which it updates in its own _Process before this child's
    void ApplyRaytracingResults()
    {
        effect = emitter.effect;

        if (emitter.IsReadyToPlay)
            UpdateFilter(emitter.MufflingGainLF, emitter.MufflingGainHF, true);
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

        emitter = null;

        base._ExitTree();
    }
}
