using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

public partial class ALSource
{
    float _volume = 1;
    float _pitch = 1;
    bool _looping = false;
    AudioStream[] _streams = [];
    bool _playbackNoRepeat = true;
    bool _autoplay = false;
    float _pitchRandomness = 1;
    float _volumeRandomnessDb = 0;

    protected void UpdateProperty<T>(ref T field, T value, Action<T, OpenALSource> updateAction) where T : struct
    {
        if (!field.Equals(value))
        {
            field = value;

            if (updateAction != null)
                foreach (var s in sources)
                    updateAction.Invoke(value, s);
        }
    }

    /// <summary>The volume of the sound</summary>
    [Export(PropertyHint.Range, "0.0,10.0")]
    public float Volume
    {
        get => _volume;
        set => UpdateProperty(ref _volume, MathF.Max(0, value), (v, source) => source.SetGain(v));
    }

    /// <summary>The pitch of the sound</summary>
    [Export(PropertyHint.Range, "0.0,10.0")]
    public float Pitch
    {
        get => _pitch;
        set => UpdateProperty(ref _pitch, MathF.Max(0, value), (v, source) => source.SetPitch(v));
    }

    /// <summary>Whether the sound plays indefinitely on loop</summary>
    [Export]
    public bool Looping
    {
        get => _looping;
        set => UpdateProperty(ref _looping, value, (v, source) => source.SetLooping(v));
    }

    /// <summary>The pool of sounds to pick from each time this source plays. Decoded on demand via Godot's own AudioStream/import pipeline and cached per-resource by ALManager.</summary>
    [Export]
    public AudioStream[] Streams
    {
        get => _streams;
        set
        {
            _streams = value ?? [];
            UpdateConfigurationWarnings();
        }
    }

    /// <summary>When true and Streams has more than one entry, the same entry is never picked twice in a row</summary>
    [Export]
    public bool PlaybackNoRepeat
    {
        get => _playbackNoRepeat;
        set => _playbackNoRepeat = value;
    }

    /// <summary>When true, this source starts playing automatically once it enters a live (non-editor) scene tree</summary>
    [Export]
    public bool Autoplay
    {
        get => _autoplay;
        set => _autoplay = value;
    }

    /// <summary>Randomises each play's actual pitch within [Pitch / PitchRandomness, Pitch * PitchRandomness]. 1.0 disables variation, matching AudioStreamRandomizer's random_pitch</summary>
    [Export(PropertyHint.Range, "1.0,4.0,0.01")]
    public float PitchRandomness
    {
        get => _pitchRandomness;
        set => _pitchRandomness = value;
    }

    /// <summary>Randomises each play's actual volume within +/- this many dB of Volume. 0.0 disables variation, matching AudioStreamRandomizer's random_volume_offset_db</summary>
    [Export(PropertyHint.Range, "0.0,24.0,0.1")]
    public float VolumeRandomnessDb
    {
        get => _volumeRandomnessDb;
        set => _volumeRandomnessDb = value;
    }

    // snake_case GDScript aliases

    /// <summary>Script-only alias for <see cref="Streams"/>, matching AudioStreamPlayer3D's single "stream" property. Lets a script written against AudioStreamPlayer3D keep working unmodified after converting to this node. Reads back the first entry of <see cref="Streams"/>; writes replace <see cref="Streams"/> with a one-entry array.</summary>
    [Export]
    public AudioStream stream
    {
        get => _streams.Length == 0 ? null : _streams[0];
        set => Streams = [value];
    }

    /// <summary>Script-only alias for <see cref="Pitch"/>, matching AudioStreamPlayer3D's "pitch_scale" property.</summary>
    [Export]
    public float pitch_scale
    {
        get => Pitch;
        set => Pitch = value;
    }

    /// <summary>Script-only alias for <see cref="Volume"/>, matching AudioStreamPlayer(3D)'s logarithmic "volume_db" property. Converts through LinearToDb/DbToLinear since <see cref="Volume"/> is linear.</summary>
    [Export]
    public float volume_db
    {
        get => Mathf.LinearToDb(Volume);
        set => Volume = Mathf.DbToLinear(value);
    }

    static readonly StringName[] scriptOnlyAliasProperties =
    [
        PropertyName.stream,
        PropertyName.pitch_scale,
        PropertyName.volume_db,
    ];

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        base._ValidateProperty(property);

        if (Array.IndexOf(scriptOnlyAliasProperties, property["name"].AsStringName()) >= 0)
            property["usage"] = (int)PropertyUsageFlags.None;
    }
}
