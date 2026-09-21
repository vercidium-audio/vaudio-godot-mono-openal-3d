using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

[Tool]
public partial class ALSource3D : ALSource
{
    float _maxDistance = 100;
    float _referenceDistance = 8;
    float _rolloffFactor = 1;

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

    /// <summary>How quickly the sound attenuates with distance beyond ReferenceDistance. Higher values fall off faster</summary>
    [Export(PropertyHint.Range, "0.0,10.0,0.1,or_greater")]
    public float RolloffFactor
    {
        get => _rolloffFactor;
        set => UpdateProperty(ref _rolloffFactor, value, (v, source) => source.SetRolloff(v));
    }

    protected override void ConfigureSource(OpenALSource source)
    {
        source.SetMaxDistance(MaxDistance);
        source.SetReferenceDistance(ReferenceDistance);
        source.SetRolloff(RolloffFactor);
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
