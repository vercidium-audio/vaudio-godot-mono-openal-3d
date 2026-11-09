namespace vaudio_godot_mono_openal;

public partial class VASource
{
    private bool _wasPlayingBeforeDeviceDestroyed = false;

    public override void _EnterTree()
    {
        base._EnterTree();

        if (!Engine.IsEditorHint())
        {
            // Register for a callback to re-play sounds when changing devices
            RegisterDeviceRecreatedCallback(OnDeviceRecreated);
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

    public override void CreateEmitter()
    {
        base.CreateEmitter();

        // Debug rendering - not part of VARaytracedSource's shared property surface (mirrors
        // VAEmitter, which also excludes these from its own base surface)
        emitter.RandomTrailColor = RandomTrailColor;
        emitter.TrailColor = TrailColor;
        emitter.OcclusionColor = OcclusionColor;
        emitter.PermeationColor = PermeationColor;
        emitter.AmbientPermeationColor = AmbientPermeationColor;
    }

    protected override void OnRaytracedByAnotherEmitter(vaudio.Emitter other)
    {
        base.OnRaytracedByAnotherEmitter(other);

        // Only autoplay once - moving to a different VAWorld creates a fresh emitter that gets raytraced again, while the sound is still playing
        PlayIfPending();
    }

    readonly VABoundsPlayback playback = new();

    // Autoplay waits for _Process, once the source is ready to play and inside the bounds
    public override void _Ready()
    {
    }

    public override bool Play()
    {
        if (playback.ApplyOnPlay(this, IsSilencedByBounds()))
            return false;

        // Wait for the muffling and reverb results, so the sound never starts unmuffled or without reverb
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

    void PlayIfPending()
    {
        if (!playback.Apply(this, IsSilencedByBounds()) && playback.IsPending(Autoplay) && IsReadyToPlay)
            Play();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (!Engine.IsEditorHint())
            PlayIfPending();
    }

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

        base._ExitTree();
    }
}
