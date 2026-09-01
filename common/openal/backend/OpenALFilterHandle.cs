namespace vaudio_godot_mono_openal;

/// <summary>
/// Wraps an <see cref="ALFilter"/> (AL_FILTER_LOWPASS) as an <see cref="IAudioFilterHandle"/>. ALFilter.SetGain already applies the "gainHF relative to gain" clamp.
/// </summary>
public class OpenALFilterHandle : IAudioFilterHandle
{
    readonly ALFilter filter;

    public OpenALFilterHandle(float gain, float gainHF) => filter = new ALFilter(gain, gainHF);

    public float Gain => filter.gain;
    public float GainHF => filter.gainHF;

    public void SetGain(float gain, float gainHF) => filter.SetGain(gain, gainHF);

    public void Delete() => filter.Delete();
}
