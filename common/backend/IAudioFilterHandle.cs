namespace vaudio_godot_mono_openal;

/// <summary>
/// A lowpass filter owned by the backend (OpenAL AL_FILTER_LOWPASS / FMOD lowpass or multiband-EQ DSP).
/// </summary>
public interface IAudioFilterHandle
{
    /// <summary>Overall gain last applied to the filter.</summary>
    float Gain { get; }

    /// <summary>High-frequency gain last applied (post the backend's relative-to-gain clamp).</summary>
    float GainHF { get; }

    /// <summary>
    /// Update the filter. <paramref name="gainHF"/> is an absolute high-frequency gain; the backend applies the "relative to gain" clamp that ALFilter.SetGain used to do.
    /// </summary>
    void SetGain(float gain, float gainHF);

    void Delete();
}
