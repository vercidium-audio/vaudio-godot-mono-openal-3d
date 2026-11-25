namespace vaudio_godot_mono_openal;

/// <summary>
/// How a sound's volume falls off with distance, between its reference and max distance. Each audio backend recreates these models
/// </summary>
public enum VADistanceModel
{
    None,
    Inverse,
    InverseClamped,
    Linear,
    LinearClamped,
    Exponent,
    ExponentClamped,
}

/// <summary>
/// The channel count and sample type of raw PCM audio, e.g. pushed into a VAStreamSource or captured by a VAInputStreamSource
/// </summary>
public enum VAPcmFormat
{
    Mono8,
    Mono16,
    Stereo8,
    Stereo16,
    MonoFloat,
    StereoFloat,
}

public static class VAPcmFormatExtensions
{
    public static int Channels(this VAPcmFormat format) => format is VAPcmFormat.Mono8 or VAPcmFormat.Mono16 or VAPcmFormat.MonoFloat ? 1 : 2;

    public static int BytesPerSample(this VAPcmFormat format) => format switch
    {
        VAPcmFormat.Mono8 or VAPcmFormat.Stereo8 => 1,
        VAPcmFormat.Mono16 or VAPcmFormat.Stereo16 => 2,
        _ => 4,
    };

    public static int BytesPerFrame(this VAPcmFormat format) => format.Channels() * format.BytesPerSample();
}

/// <summary>
/// Low and high frequency gains (0-1) applied to a voice's direct path or reverb send, from the SDK's muffling results
/// </summary>
public readonly record struct VALowPass(float GainLF, float GainHF)
{
    public static readonly VALowPass Full = new(1, 1);
    public static readonly VALowPass Silence = new(0, 0);

    /// <summary>GainHF relative to GainLF, clamped to 1, as the nodes' GainHF properties report it</summary>
    public float RelativeGainHF => MathF.Min(1, GainHF / MathF.Max(0.01f, GainLF));
}

public enum VAVoiceState
{
    Initial,
    Playing,
    Paused,
    Stopped,
}
