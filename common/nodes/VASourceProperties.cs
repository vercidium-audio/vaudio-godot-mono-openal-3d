namespace vaudio_godot_mono_openal;

public partial class VASource
{
    [ExportGroup("Debug Rendering")]

    bool _RandomTrailColor = false;
    /// <summary>If true, renders each trail a different color in the debug window (dev build only)</summary>
    [Export]
    public bool RandomTrailColor
    {
        get => _RandomTrailColor;
        set
        {
            _RandomTrailColor = value;

            if (emitter != null)
                emitter.RandomTrailColor = value;
        }
    }

    Godot.Color _TrailColor = new(1, 1, 1, 0.1f);
    /// <summary>The color of ray trails in the debug window (dev build only)</summary>
    [Export]
    public Godot.Color TrailColor
    {
        get => _TrailColor;
        set
        {
            _TrailColor = value;

            if (emitter != null)
                emitter.TrailColor = value;
        }
    }

    Godot.Color _ReverbColor = new(0.11f, 0.97f, 1.0f, 0.2f);
    /// <summary>The color of reverb rays in the debug window (dev build only)</summary>
    [Export]
    public Godot.Color ReverbColor
    {
        get => _ReverbColor;
        set
        {
            _ReverbColor = value;

            if (emitter != null)
                emitter.ReverbColor = value;
        }
    }

    Godot.Color _OcclusionColor = new(0.44f, 1.0f, 0.64f, 0.2f);
    /// <summary>The color of occlusion rays in the debug window (dev build only)</summary>
    [Export]
    public Godot.Color OcclusionColor
    {
        get => _OcclusionColor;
        set
        {
            _OcclusionColor = value;

            if (emitter != null)
                emitter.OcclusionColor = value;
        }
    }

    Godot.Color _PermeationColor = new(1.0f, 0.5f, 0.17f, 0.2f);
    /// <summary>The color of permeation rays in the debug window (dev build only)</summary>
    [Export]
    public Godot.Color PermeationColor
    {
        get => _PermeationColor;
        set
        {
            _PermeationColor = value;

            if (emitter != null)
                emitter.PermeationColor = value;
        }
    }

    Godot.Color _AmbientPermeationColor = new(1.0f, 0.8f, 0.0f, 0.2f);
    /// <summary>The color of ambientPermeation rays in the debug window (dev build only)</summary>
    [Export]
    public Godot.Color AmbientPermeationColor
    {
        get => _AmbientPermeationColor;
        set
        {
            _AmbientPermeationColor = value;

            if (emitter != null)
                emitter.AmbientPermeationColor = value;
        }
    }
}
