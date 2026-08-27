namespace vaudio_godot_mono_openal;

[Tool]
[GlobalClass]
public partial class VASourceRelative : ALSourceRelative
{
    private VAWorld vercidiumAudio;

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

        base._ExitTree();
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
