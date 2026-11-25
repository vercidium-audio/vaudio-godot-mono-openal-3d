namespace vaudio_godot_mono_openal;

// Shared body of VASourceRelative. Godot binds the script to nodes/VASourceRelative.cs (the file named after the class), which must stay at that path so scenes saved by older versions still load
public partial class VASourceRelative
{
    private VAWorld vercidiumAudio;

    Action cancelWaitForVAWorld;

    // Never silenced by the bounds, as it plays at the listener
    readonly VABoundsPlayback playback = new();

    bool warnedNoListenerReverb = false;

    /// <summary>If true, this source is sent to the listener's reverb bus, and waits for the listener's first raytraced reverb before it plays. If false, it plays straight away with no reverb (e.g. music, UI), and needs no VAWorld</summary>
    [Export]
    public bool UseListenerReverb { get; set; } = true;

    /// <summary>
    /// True once the current listener's reverb has its first raytraced results, or never will (no reverb rays, or outside the bounds). Sources don't play until this is true, so they never start in the backend's default reverb. Always true when <see cref="UseListenerReverb"/> is off
    /// </summary>
    public bool IsReadyToPlay => !UseListenerReverb || (vercidiumAudio != null && vercidiumAudio.IsListenerReverbReady());

    protected override void ConfigureVoice(VAVoice voice) => ConfigureListenerRelativeVoice(voice);

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

    // Autoplay waits for _Process, once the source is ready to play
    public override void _Ready()
    {
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (!Engine.IsEditorHint() && playback.Process(this, false, IsReadyToPlay, Autoplay))
            Play(playback.PendingStartTime);
    }

    // Waits until the source is ready to play, and returns true while waiting
    public override bool Play(float startTime)
    {
        if (Engine.IsEditorHint())
            return false;

        if (!playback.RequestPlay(this, false, IsReadyToPlay, startTime))
            return true;

        // Send to the listener reverb bus (or none), with no filter
        if (UseListenerReverb)
            vercidiumAudio.WarnIfListenerCastsNoReverbRays(this, ref warnedNoListenerReverb);

        reverbBus = UseListenerReverb ? vercidiumAudio.listenerReverbBus : null;
        UpdateFilter(1, 1);

        var played = base.Play(startTime);

        if (played)
            playback.OnPlayed();

        return played;
    }

    // Also cancels a play or autoplay that's waiting
    public override void Stop()
    {
        playback.Cancel();
        base.Stop();
    }
}
