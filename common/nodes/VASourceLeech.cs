// Human-created

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
    public float GainLF => mufflingFilter?.GainLF ?? 0;
    public float GainHF => mufflingFilter?.RelativeGainHF ?? 0;

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
            LogWarning($"{Name}: This source will never play. VASourceLeech nodes can't be a child of a VAListener node. Use a VASourceRelative instead.");
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

    // Matches VASource's Play() override: waits until the parent emitter is ready to play, and follows the same bounds rules
    public override bool Play(float startTime)
    {
        var silenced = IsSilencedByBounds();

        // Returns true while waiting for the parent emitter to be ready, and false while silenced by the bounds, even for a loop that will play once inside
        if (!playback.RequestPlay(this, silenced, IsReadyToPlay, startTime))
            return !silenced;

        var played = base.Play(startTime);

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

        // Stops the sound while the parent emitter is outside the bounds, and starts a queued play or autoplay once it's ready
        if (playback.Process(this, IsSilencedByBounds(), IsReadyToPlay, Autoplay))
            Play(playback.PendingStartTime);

        if (!IsReadyToPlay)
            return;

        // Copies the parent VAEmitter's muffling and reverb, which it updates in its own _Process before this child's. The effect is null if it casts no reverb rays
        reverbEffect = emitter.reverbEffect;
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
        VAAudioBackend.UnregisterDeviceRecreatedCallback(OnDeviceRecreated);

        emitter = null;

        base._ExitTree();
    }
}
