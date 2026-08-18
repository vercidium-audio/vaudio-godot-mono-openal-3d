using godot_mono_openal;

namespace vaudio_godot_mono_openal_3d;

[GlobalClass]
public partial class VASourceRelative : ALSource3D
{
    private VAWorld vercidiumAudio;

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

        Relative = true;
    }

    public override bool Play()
    {
        if (Engine.IsEditorHint())
            return false;

        // Set the effect, with no filter
        effect = vercidiumAudio.listenerReverbEffect;
        UpdateFilter(1, 1);

        return base.Play();
    }
}
