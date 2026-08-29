using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

[Tool]
public partial class ALSource3D : ALSource
{
    float _maxDistance = 100;
    float _referenceDistance = 8;

    /// <summary>The max distance that the sound can be heard at. Also affected by the falloff model in <see cref="ALManager"/></summary>
    [Export]
    public float MaxDistance
    {
        get => _maxDistance;
        set => UpdateProperty(ref _maxDistance, MathF.Max(0, value), (v, source) => source.SetMaxDistance(v));
    }

    /// <summary>The distance that sound volume falloff starts at</summary>
    [Export]
    public float ReferenceDistance
    {
        get => _referenceDistance;
        set => UpdateProperty(ref _referenceDistance, MathF.Max(0, value), (v, source) => source.SetReferenceDistance(v));
    }

    protected override void ConfigureSource(OpenALSource source)
    {
        source.SetMaxDistance(MaxDistance);
        source.SetReferenceDistance(ReferenceDistance);
        source.SetPosition(GlobalPosition);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Engine.IsEditorHint())
            return;

        foreach (var s in sources)
            s.SetPosition(GlobalPosition);
    }
}
