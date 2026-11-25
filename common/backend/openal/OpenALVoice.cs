// AI-created

using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

// An AL source, with its own direct and send filters
sealed class OpenALVoice(OpenALSource source, bool isStream = false) : VAVoice
{
    internal readonly OpenALSource Source = source;

    ALFilter directFilter;
    ALFilter sendFilter;

    public override bool IsDisposed => Source.IsDisposed();

    public override VAVoiceState State => AL.GetSourcei(Source.ID, AL.AL_SOURCE_STATE) switch
    {
        AL.AL_INITIAL => VAVoiceState.Initial,
        AL.AL_PLAYING => VAVoiceState.Playing,
        AL.AL_PAUSED => VAVoiceState.Paused,
        _ => VAVoiceState.Stopped,
    };

    public override bool IsFinished => Source.Finished();

    public override void Play() => Source.Play();
    public override void Pause() => AL.SourcePause(Source.ID);
    public override void Stop() => Source.Stop();

    // A stream's callback buffer has no fixed length
    public override float Length => isStream ? 0 : BufferLength((uint)AL.GetSourcei(Source.ID, AL.AL_BUFFER));

    public override float PlaybackPosition
    {
        get => AL.GetSourcef(Source.ID, AL.AL_SEC_OFFSET);
        set => Source.SetSecOffset(value);
    }

    public override bool Looping
    {
        get => Source.looping;
        set => Source.SetLooping(value);
    }

    public override void SetGain(float gain) => Source.SetGain(gain);
    public override void SetPitch(float pitch) => Source.SetPitch(pitch);
    public override void SetRelative(bool relative) => Source.SetRelative(relative);
    public override void SetSpatialised(bool spatialised) => Source.SetSpatialise(spatialised);
    public override void SetPosition(Vector3 position) => Source.SetPosition([position.X, position.Y, position.Z]);
    public override void SetVelocity(Vector3 velocity) => Source.SetVelocity([velocity.X, velocity.Y, velocity.Z]);
    public override void SetReferenceDistance(float distance) => Source.SetReferenceDistance(distance);
    public override void SetMaxDistance(float distance) => Source.SetMaxDistance(distance);
    public override void SetRolloff(float rolloff) => Source.SetRolloff(rolloff);

    public override void SetFilters(VALowPassFilter direct, VAReverbEffect reverbEffect, VALowPassFilter send)
    {
        UpdateFilter(ref directFilter, direct);
        UpdateFilter(ref sendFilter, send);

        Source.SetFilter((reverbEffect as OpenALReverbEffect)?.Effect, directFilter, sendFilter);
    }

    // ALFilter.SetGain takes an absolute gainHF and converts it to AL_LOWPASS_GAINHF's relative value itself
    static void UpdateFilter(ref ALFilter filter, VALowPassFilter lowPass)
    {
        if (filter == null)
            filter = new(lowPass.GainLF, lowPass.GainHF);
        else
            filter.SetGain(lowPass.GainLF, lowPass.GainHF);
    }

    public override void Dispose()
    {
        if (!Source.IsDisposed())
            Source.Dispose();

        // Filters must be deleted after the source that uses them
        directFilter?.Delete();
        directFilter = null;

        sendFilter?.Delete();
        sendFilter = null;
    }

    internal static float BufferLength(uint buffer)
    {
        if (buffer == 0)
            return 0;

        var bytesPerFrame = AL.GetBufferi(buffer, AL.AL_CHANNELS) * AL.GetBufferi(buffer, AL.AL_BITS) / 8;
        var frequency = AL.GetBufferi(buffer, AL.AL_FREQUENCY);

        if (bytesPerFrame <= 0 || frequency <= 0)
            return 0;

        return (float)AL.GetBufferi(buffer, AL.AL_SIZE) / bytesPerFrame / frequency;
    }
}

// An AL source fed by alBufferCallbackSOFT
sealed class OpenALStream(ALStreamSource stream) : VAStream
{
    readonly OpenALVoice voice = new(stream, isStream: true);

    public override VAVoice Voice => voice;

    public override void Enqueue(byte[] data, int offset, int length) => stream.EnqueueData(data, offset, length);

    public override void ReleasePlayedChunks()
    {
        while (!stream.IsDisposed() && stream.TryGetUsedData(out _))
        {
        }
    }
}
