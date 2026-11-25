// AI-created

using System.Runtime.InteropServices;

namespace vaudio_godot_mono_openal;

// An FMOD channel, with a THREE_EQ DSP as its muffling filter. Created paused, so it stays Initial until played
sealed unsafe class FMODVoice : VAVoice
{
    // Matches the EFX reference frequencies the SDK's GainLF and GainHF describe
    const float LowCrossover = 250;
    const float HighCrossover = 5000;

    // THREE_EQ's lowest gain
    const float SilenceDecibels = -80;

    readonly FMODBackend backend;
    FMOD.Channel channel;
    FMOD.DSP eq;

    readonly float sampleRate;
    readonly float length;

    // A stream's voice owns its sound and always loops, as FMOD keeps reading the stream
    FMOD.Sound streamSound;
    readonly bool isStream;

    // FMOD keeps this pointer rather than copying the curve, so it lives until the voice is disposed
    FMOD.VECTOR* rolloffPoints;

    bool disposed;
    bool played;
    bool looping;
    bool relative;
    bool spatialised;
    Vector3 position;
    Vector3 velocity;
    float referenceDistance = 1;
    float maxDistance = 1000;
    float rolloff = 1;
    VALowPassFilter? direct;

    internal FMODVoice(FMODBackend backend, FMOD.Channel channel, float sampleRate, float length, bool spatialised, FMOD.Sound streamSound = default)
    {
        this.backend = backend;
        this.channel = channel;
        this.sampleRate = sampleRate;
        this.length = length;
        this.spatialised = spatialised;
        this.streamSound = streamSound;
        isStream = streamSound.hasHandle();
        relative = !spatialised;

        rolloffPoints = (FMOD.VECTOR*)NativeMemory.Alloc(FMODRolloff.PointCount, (nuint)sizeof(FMOD.VECTOR));

        Call(channel.setLoopCount(-1), "Channel::setLoopCount");
        ApplyMode();
        ApplyRolloff();

        if (backend.Check(backend.System.createDSPByType(FMOD.DSP_TYPE.THREE_EQ, out eq), "System::createDSPByType"))
        {
            backend.Check(eq.setParameterFloat((int)FMOD.DSP_THREE_EQ.LOWCROSSOVER, LowCrossover), "DSP::setParameterFloat");
            backend.Check(eq.setParameterFloat((int)FMOD.DSP_THREE_EQ.HIGHCROSSOVER, HighCrossover), "DSP::setParameterFloat");
            Call(channel.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, eq), "Channel::addDSP");
        }

        backend.AddVoice(this);
    }

    bool HasChannel => !disposed && channel.hasHandle() && backend.IsInitialised;

    // A channel that finished, or was stolen for a more important sound, is invalid. That's how FMOD reports a stopped voice, so it isn't an error
    bool Call(FMOD.RESULT result, string call)
    {
        if (result is FMOD.RESULT.ERR_INVALID_HANDLE or FMOD.RESULT.ERR_CHANNEL_STOLEN)
        {
            channel.clearHandle();
            return false;
        }

        return backend.Check(result, call);
    }

    public override bool IsDisposed => disposed;

    public override VAVoiceState State
    {
        get
        {
            if (!HasChannel || !Call(channel.isPlaying(out bool playing), "Channel::isPlaying") || !playing)
                return VAVoiceState.Stopped;

            if (!Call(channel.getPaused(out bool paused), "Channel::getPaused"))
                return VAVoiceState.Stopped;

            if (!paused)
                return VAVoiceState.Playing;

            return played ? VAVoiceState.Paused : VAVoiceState.Initial;
        }
    }

    public override bool IsFinished => !looping && State == VAVoiceState.Stopped;

    public override void Play()
    {
        if (!HasChannel)
            return;

        played = true;
        Call(channel.setPaused(false), "Channel::setPaused");
    }

    public override void Pause()
    {
        if (HasChannel)
            Call(channel.setPaused(true), "Channel::setPaused");
    }

    public override void Stop()
    {
        if (HasChannel)
            Call(channel.stop(), "Channel::stop");

        channel.clearHandle();
    }

    public override float Length => length;

    public override float PlaybackPosition
    {
        get => HasChannel && Call(channel.getPosition(out uint frames, FMOD.TIMEUNIT.PCM), "Channel::getPosition") ? frames / sampleRate : 0;
        set
        {
            if (HasChannel)
                Call(channel.setPosition((uint)(MathF.Max(0, value) * sampleRate), FMOD.TIMEUNIT.PCM), "Channel::setPosition");
        }
    }

    public override bool Looping
    {
        get => looping;
        set
        {
            looping = value;
            ApplyMode();
        }
    }

    public override void SetGain(float gain)
    {
        if (HasChannel)
            Call(channel.setVolume(gain), "Channel::setVolume");
    }

    public override void SetPitch(float pitch)
    {
        if (HasChannel)
            Call(channel.setPitch(pitch), "Channel::setPitch");
    }

    public override void SetRelative(bool relative)
    {
        this.relative = relative;
        ApplyMode();
    }

    public override void SetSpatialised(bool spatialised)
    {
        this.spatialised = spatialised;
        ApplyMode();
    }

    public override void SetPosition(Vector3 position)
    {
        this.position = position;
        Apply3DAttributes();
    }

    public override void SetVelocity(Vector3 velocity)
    {
        this.velocity = velocity;
        Apply3DAttributes();
    }

    public override void SetReferenceDistance(float distance)
    {
        referenceDistance = distance;
        ApplyRolloff();
    }

    public override void SetMaxDistance(float distance)
    {
        maxDistance = distance;
        ApplyRolloff();
    }

    public override void SetRolloff(float rolloff)
    {
        this.rolloff = rolloff;
        ApplyRolloff();
    }

    // TEMP: reverb sends are added with FMOD reverb effects
    public override void SetFilters(VALowPassFilter direct, VAReverbEffect reverbEffect, VALowPassFilter send)
    {
        if (this.direct == direct || !HasChannel || !eq.hasHandle())
            return;

        this.direct = direct;

        // The mid band gets the average of the two, like vaudio-fmod's FMODSound
        backend.Check(eq.setParameterFloat((int)FMOD.DSP_THREE_EQ.LOWGAIN, ToDecibels(direct.GainLF)), "DSP::setParameterFloat");
        backend.Check(eq.setParameterFloat((int)FMOD.DSP_THREE_EQ.MIDGAIN, ToDecibels((direct.GainLF + direct.GainHF) / 2)), "DSP::setParameterFloat");
        backend.Check(eq.setParameterFloat((int)FMOD.DSP_THREE_EQ.HIGHGAIN, ToDecibels(direct.GainHF)), "DSP::setParameterFloat");
    }

    static float ToDecibels(float gain) => gain <= 0 ? SilenceDecibels : Math.Clamp(20 * MathF.Log10(gain), SilenceDecibels, 0);

    void ApplyMode()
    {
        if (!HasChannel)
            return;

        var mode = (spatialised ? FMOD.MODE._3D : FMOD.MODE._2D) | (relative ? FMOD.MODE._3D_HEADRELATIVE : FMOD.MODE._3D_WORLDRELATIVE) | FMOD.MODE._3D_CUSTOMROLLOFF | (looping || isStream ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF);

        Call(channel.setMode(mode), "Channel::setMode");
    }

    void Apply3DAttributes()
    {
        if (!HasChannel)
            return;

        var fmodPosition = FMODBackend.ToFMOD(position);
        var fmodVelocity = FMODBackend.ToFMOD(velocity);
        Call(channel.set3DAttributes(ref fmodPosition, ref fmodVelocity), "Channel::set3DAttributes");
    }

    // Also called by the backend when the distance model changes. FMOD ignores the min/max distance with a custom curve
    internal void ApplyRolloff()
    {
        if (!HasChannel)
            return;

        FMODRolloff.Fill(rolloffPoints, backend.DistanceModel, referenceDistance, maxDistance, rolloff);
        Call(channel.set3DCustomRolloff(ref *rolloffPoints, FMODRolloff.PointCount), "Channel::set3DCustomRolloff");
    }

    public override void Dispose()
    {
        if (disposed)
            return;

        if (HasChannel)
        {
            if (eq.hasHandle())
                Call(channel.removeDSP(eq), "Channel::removeDSP");

            Call(channel.stop(), "Channel::stop");
        }

        // System::release already freed these with the device
        if (backend.IsInitialised)
        {
            if (eq.hasHandle())
                backend.Check(eq.release(), "DSP::release");

            if (streamSound.hasHandle())
                backend.Check(streamSound.release(), "Sound::release");
        }

        disposed = true;
        channel.clearHandle();
        eq.clearHandle();
        streamSound.clearHandle();
        backend.RemoveVoice(this);

        NativeMemory.Free(rolloffPoints);
        rolloffPoints = null;
    }
}

// A user-created FMOD stream sound, whose read callback pulls from the queue on FMOD's stream thread
sealed unsafe class FMODStream : VAStream
{
    // Short, so pushed audio isn't heard late
    const float DecodeBufferSeconds = 0.05f;

    readonly VAStreamQueue queue;

    // FMOD's PCM8 is signed, VAPcmFormat's 8-bit formats are unsigned like OpenAL's
    readonly bool unsigned8;

    // Kept alive for as long as FMOD can call it
    readonly FMOD.SOUND_PCMREAD_CALLBACK readCallback;

    FMODVoice voice;

    public override VAVoice Voice => voice;

    FMODStream(VAPcmFormat format)
    {
        queue = new(format);
        unsigned8 = format.BytesPerSample() == 1;
        readCallback = Read;
    }

    FMOD.RESULT Read(IntPtr sound, IntPtr data, uint length)
    {
        var bytes = (byte*)data;
        queue.Read(bytes, (int)length);

        if (unsigned8)
        {
            for (uint i = 0; i < length; i++)
                bytes[i] ^= 0x80;
        }

        return FMOD.RESULT.OK;
    }

    // Returns null if FMOD failed, which it logs
    internal static FMODStream Create(FMODBackend backend, VAPcmFormat format, int sampleRate)
    {
        var stream = new FMODStream(format);

        var exinfo = new FMOD.CREATESOUNDEXINFO
        {
            cbsize = Marshal.SizeOf<FMOD.CREATESOUNDEXINFO>(),
            numchannels = format.Channels(),
            defaultfrequency = sampleRate,
            format = format.BytesPerSample() switch
            {
                1 => FMOD.SOUND_FORMAT.PCM8,
                2 => FMOD.SOUND_FORMAT.PCM16,
                _ => FMOD.SOUND_FORMAT.PCMFLOAT,
            },
            decodebuffersize = (uint)Math.Max(256, sampleRate * DecodeBufferSeconds),

            // One second, looped forever
            length = (uint)(sampleRate * format.BytesPerFrame()),
            pcmreadcallback = stream.readCallback,
        };

        var mode = FMOD.MODE.OPENUSER | FMOD.MODE.CREATESTREAM | FMOD.MODE.LOOP_NORMAL | FMOD.MODE._3D;

        if (!backend.Check(backend.System.createSound(IntPtr.Zero, mode, ref exinfo, out var sound), "System::createSound"))
            return null;

        stream.voice = backend.PlaySound(sound, sampleRate, 0, true, sound);

        if (stream.voice == null)
        {
            backend.Check(sound.release(), "Sound::release");
            return null;
        }

        stream.voice.Play();
        return stream;
    }

    public override void Enqueue(byte[] data, int offset, int length) => queue.Enqueue(data, offset, length);
}
