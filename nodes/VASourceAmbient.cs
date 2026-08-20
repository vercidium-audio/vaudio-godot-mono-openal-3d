using godot_mono_openal;

namespace vaudio_godot_mono_openal_3d;

[Tool]
[GlobalClass]
public partial class VASourceAmbient : ALSourceRelative
{
    private VAWorld vercidiumAudio;
    private bool played = false;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
            return;

        vercidiumAudio = this.GetVAWorldParent();
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

        effect = null;// vercidiumAudio.listenerReverbEffect;
        UpdateFilter(vercidiumAudio.ambientFilter.gain, vercidiumAudio.ambientFilter.gainHF);

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
