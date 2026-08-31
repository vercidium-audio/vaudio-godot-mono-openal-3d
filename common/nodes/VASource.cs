namespace vaudio_godot_mono_openal;

public partial class VASource
{
    private bool _PlayWhenRaytracingCompletes = true;
    private bool _wasPlayingBeforeDeviceDestroyed = false;

    [Export]
    public bool PlayWhenRaytracingCompletes
    {
        get => _PlayWhenRaytracingCompletes;
        set => _PlayWhenRaytracingCompletes = value;
    }

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

        if (PlayWhenRaytracingCompletes)
            Play();
    }

    bool played = false;

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

        if (Raytraced && !played && PlayWhenRaytracingCompletes)
            Play();
    }

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

        base._ExitTree();
    }
}
