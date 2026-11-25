namespace vaudio_godot_mono_openal;

public partial class VAPlayer
{
    // Signals
    [Signal]
    public delegate void FinishedEventHandler();

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        for (int i = 0; i < Streams.Count; i++)
        {
            if (StreamAt(i) == null)
                warnings.Add($"Streams[{i}] is not set");
        }

        return warnings.ToArray();
    }

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
            return;

        VAAudioBackend.Ensure();
    }

    public override void _Ready()
    {
        if (Autoplay && !Engine.IsEditorHint())
            Play();
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
            return;

        if (playRequested)
            Play(requestedStartTime);

        for (int i = voices.Count - 1; i >= 0; i--)
        {
            var voice = voices[i];

            if (voice.IsFinished)
            {
                voice.Dispose();
                voices.RemoveAt(i);

                if (voices.Count == 0)
                    EmitSignal(SignalName.Finished);
            }
        }

        // Picks up this source's or the VAWorld's ReverbOnly changing, including on sources that don't update their filter every frame
        if (UpdateDryOutput())
        {
            foreach (var voice in voices)
                ApplyFilters(voice);
        }
    }

    // Released on free rather than _ExitTree, so a reparent keeps playing (matches the native plugin, which releases them in its destructor)
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
            DisposeVoices();
    }
}
