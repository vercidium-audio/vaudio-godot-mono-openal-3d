using OpenALManagedSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

/// <summary>
/// Wraps an <see cref="OpenAL.managed.ALSource"/> as an <see cref="IAudioSourceHandle"/>.
///
/// Owns two lowpass filters - one for the direct (dry) path and one for the reverb send - built lazily from the <see cref="AudioFilter"/> DTOs passed to <see cref="ApplyReverb"/>. This mirrors what AudioSource.cs does today with its per-node <c>filter</c> plus the static silence/full filters, but keeps the filter lifetime tied to the voice.
/// </summary>
public class OpenALSourceHandle : IAudioSourceHandle
{
    readonly OpenALManagedSource source;

    ALFilter directFilter;
    ALFilter reverbSendFilter;

    public OpenALSourceHandle(OpenALManagedSource source) => this.source = source;

    public void SetGain(float gain) => source.SetGain(gain);
    public void SetPitch(float pitch) => source.SetPitch(pitch);
    public void SetLooping(bool looping) => source.SetLooping(looping);

    public void SetPosition(Vector3 position) => AL.Sourcefv(source.ID, AL.AL_POSITION, [position.X, position.Y, position.Z]);

    public void SetRelative(bool relative) => source.SetRelative(relative);

    public void SetMaxDistance(float distance) => source.SetMaxDistance(distance);
    public void SetReferenceDistance(float distance) => source.SetReferenceDistance(distance);

    public void ApplyReverb(IAudioReverbSlot slot, AudioFilter direct, AudioFilter reverbSend)
    {
        UpdateFilter(ref directFilter, direct);
        UpdateFilter(ref reverbSendFilter, reverbSend);

        var effect = (slot as OpenALReverbSlot)?.Effect;
        source.SetFilter(effect, directFilter, reverbSendFilter);
    }

    static void UpdateFilter(ref ALFilter filter, AudioFilter dto)
    {
        if (filter == null)
            filter = new ALFilter(dto.gain, dto.gainHF);
        else
            filter.SetGain(dto.gain, dto.gainHF);
    }

    public void Play() => source.Play();
    public void Stop() => source.Stop();
    public bool Finished() => source.Finished();

    public void Dispose()
    {
        if (!source.IsDisposed())
            source.Dispose();

        directFilter?.Delete();
        directFilter = null;

        reverbSendFilter?.Delete();
        reverbSendFilter = null;
    }
}
