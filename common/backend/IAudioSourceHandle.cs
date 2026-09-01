namespace vaudio_godot_mono_openal;

/// <summary>
/// One playing voice spawned from an <see cref="IAudioBuffer"/>.
/// </summary>
public interface IAudioSourceHandle
{
    void SetGain(float gain);
    void SetPitch(float pitch);
    void SetLooping(bool looping);

    /// <summary>World position of the voice. Ignored while the voice is listener-relative.</summary>
    void SetPosition(Vector3 position);

    /// <summary>When true the voice's position is relative to the listener (used for non-spatialised / listener-attached sounds).</summary>
    void SetRelative(bool relative);

    void SetMaxDistance(float distance);
    void SetReferenceDistance(float distance);

    /// <summary>
    /// Route this voice through a reverb slot. <paramref name="direct"/> filters the dry path, <paramref name="reverbSend"/> filters the signal sent into the reverb. Pass <see cref="AudioFilter.Silent"/> / <see cref="AudioFilter.Full"/> for the trivial cases.
    /// </summary>
    void ApplyReverb(IAudioReverbSlot slot, AudioFilter direct, AudioFilter reverbSend);

    void Play();
    void Stop();
    bool Finished();
    void Dispose();
}
