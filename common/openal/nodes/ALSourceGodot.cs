namespace vaudio_godot_mono_openal;

public partial class ALSource
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

        ALManager.Ensure();
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
            Play();

        for (int i = sources.Count - 1; i >= 0; i--)
        {
            var s = sources[i];

            if (s.Finished())
            {
                s.Dispose();
                sources.RemoveAt(i);

                if (sources.Count == 0)
                    EmitSignal(SignalName.Finished);

                continue;
            }
        }
    }

    public override void _ExitTree()
    {
        filter?.Delete();

        foreach (var s in sources)
            s.Dispose();

        sources.Clear();
    }
}
