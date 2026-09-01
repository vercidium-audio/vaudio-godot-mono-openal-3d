namespace vaudio_godot_mono_openal;

[Tool]
[GlobalClass]
public partial class VASourceAmbient : AudioSourceRelative
{
    private VAWorld vercidiumAudio;
    private bool played = false;

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
            AudioManager.UnregisterDeviceRecreatedCallback(OnDeviceRecreated);

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

    public override bool Play()
    {
        // Don't play until we've raytraced once
        if (vercidiumAudio?.ambientFilter == null)
            return false;

        played = base.Play();
        return played;
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
            return;

        // Ensure VAWorld is available
        if (vercidiumAudio?.ambientFilter == null)
            return;

        effect = null;// vercidiumAudio.listenerReverbSlot;
        UpdateFilter(vercidiumAudio.ambientFilter.Gain, vercidiumAudio.ambientFilter.GainHF);

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
