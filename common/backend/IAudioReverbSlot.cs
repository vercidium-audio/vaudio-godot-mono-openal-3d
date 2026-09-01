namespace vaudio_godot_mono_openal;

/// <summary>
/// A reverb instance a voice can be routed into. The backend owns the underlying effect (OpenAL aux effect slot / FMOD reverb instance or SFXREVERB DSP).
/// </summary>
public interface IAudioReverbSlot
{
    /// <summary>Push updated reverb parameters. The backend copies what it can from the DTO and applies the change.</summary>
    void Push(AudioReverbEffect effect);

    void Dispose();
}
