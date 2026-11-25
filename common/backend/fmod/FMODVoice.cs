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
    internal const float SilenceDecibels = -80;

    readonly FMODBackend backend;
    FMOD.Channel channel;

    // The direct path's muffling filter
    FMOD.DSP eq;

    // The reverb send and its filter, before eq. Created when the voice first sends to a reverb effect
    FMOD.DSP send;
    FMOD.DSP sendEQ;
    int sendReturnID = -1;
    FMODReverbEffect sendEffect;
    float sendLevel = float.NaN;
    VALowPassFilter preSend = VALowPassFilter.Full;

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
    float gain = 1;

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

        AddEQ(out eq);

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
        this.gain = gain;

        if (!HasChannel)
            return;

        Call(channel.setVolume(gain), "Channel::setVolume");
        UpdateSendLevel();
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

    // The DSP chain from the input end is sendEQ -> send -> eq -> fader. The send comes before the fader and FMOD's 3D rolloff, so it carries the voice's gain and its rolloff curve's distance attenuation itself, and the direct path's silence or extra filtering in eq never reaches it
    public override void SetFilters(VALowPassFilter direct, VAReverbEffect reverbEffect, VALowPassFilter send)
    {
        if (!HasChannel || !eq.hasHandle())
            return;

        var effect = reverbEffect as FMODReverbEffect;
        int returnID = effect != null && effect.IsValid ? effect.ReturnID : -1;

        if (returnID >= 0 && !this.send.hasHandle() && !AddSend())
            returnID = -1;

        sendEffect = returnID >= 0 ? effect : null;

        if (returnID != sendReturnID)
        {
            sendReturnID = returnID;
            backend.Check(this.send.setParameterInt((int)FMOD.DSP_SEND.RETURNID, returnID), "DSP::setParameterInt");
        }

        UpdateSendLevel();

        // Without a send, eq does all the filtering
        var preSend = returnID >= 0 ? send : VALowPassFilter.Full;

        if (this.preSend != preSend && sendEQ.hasHandle())
        {
            SetEQ(sendEQ, ToDecibels(preSend.GainLF), ToDecibels(Mid(preSend)), ToDecibels(preSend.GainHF));
            backend.Check(sendEQ.setBypass(preSend == VALowPassFilter.Full), "DSP::setBypass");
        }

        if (this.direct == direct && this.preSend == preSend)
            return;

        this.direct = direct;
        this.preSend = preSend;

        // eq comes after sendEQ, so it only applies what's left of the direct filter
        SetEQ(eq, RemainingDecibels(direct.GainLF, preSend.GainLF), RemainingDecibels(Mid(direct), Mid(preSend)), RemainingDecibels(direct.GainHF, preSend.GainHF));
    }

    // The mid band gets the average of the two, like vaudio-fmod's FMODSound
    static float Mid(VALowPassFilter filter) => (filter.GainLF + filter.GainHF) / 2;

    static float RemainingDecibels(float gain, float alreadyApplied) => Math.Clamp(ToDecibels(gain) - ToDecibels(alreadyApplied), SilenceDecibels, 0);

    void SetEQ(FMOD.DSP dsp, float low, float mid, float high)
    {
        backend.Check(dsp.setParameterFloat((int)FMOD.DSP_THREE_EQ.LOWGAIN, low), "DSP::setParameterFloat");
        backend.Check(dsp.setParameterFloat((int)FMOD.DSP_THREE_EQ.MIDGAIN, mid), "DSP::setParameterFloat");
        backend.Check(dsp.setParameterFloat((int)FMOD.DSP_THREE_EQ.HIGHGAIN, high), "DSP::setParameterFloat");
    }

    static float ToDecibels(float gain) => gain <= 0 ? SilenceDecibels : Math.Clamp(20 * MathF.Log10(gain), SilenceDecibels, 0);

    // Adds a THREE_EQ at the input end of the chain
    bool AddEQ(out FMOD.DSP dsp)
    {
        if (!backend.Check(backend.System.createDSPByType(FMOD.DSP_TYPE.THREE_EQ, out dsp), "System::createDSPByType"))
            return false;

        backend.Check(dsp.setParameterFloat((int)FMOD.DSP_THREE_EQ.LOWCROSSOVER, LowCrossover), "DSP::setParameterFloat");
        backend.Check(dsp.setParameterFloat((int)FMOD.DSP_THREE_EQ.HIGHCROSSOVER, HighCrossover), "DSP::setParameterFloat");
        return AddToChannel(ref dsp);
    }

    // Adds the SEND and its EQ the first time the voice sends to a reverb effect. Most voices never do
    bool AddSend()
    {
        if (!backend.Check(backend.System.createDSPByType(FMOD.DSP_TYPE.SEND, out send), "System::createDSPByType") || !AddToChannel(ref send))
            return false;

        if (AddEQ(out sendEQ))
            backend.Check(sendEQ.setBypass(true), "DSP::setBypass");

        return true;
    }

    // Releases the DSP if it couldn't be added
    bool AddToChannel(ref FMOD.DSP dsp)
    {
        if (Call(channel.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, dsp), "Channel::addDSP"))
            return true;

        backend.Check(dsp.release(), "DSP::release");
        dsp.clearHandle();
        return false;
    }

    internal override float ReverbSendLevel => sendEffect != null && send.hasHandle() ? sendLevel : -1;

    // Called whenever the gain or effect changes, and every frame by the backend, since the voice's or listener's position may have moved
    internal void UpdateSendLevel()
    {
        if (!HasChannel || !send.hasHandle())
            return;

        float level = Math.Clamp(gain, 0, 1) * SendDistanceGain();

        if (level == sendLevel)
            return;

        sendLevel = level;
        backend.Check(send.setParameterFloat((int)FMOD.DSP_SEND.LEVEL, level), "DSP::setParameterFloat");
    }

    // The same attenuation the voice's own rolloff curve gives the direct path. A relative voice's distance is from the listener, and an unspatialised one (FMOD's 2D mode) isn't attenuated
    float SendDistanceGain()
    {
        if (!spatialised)
            return 1;

        float distance = relative ? position.Length() : position.DistanceTo(backend.ListenerPosition);
        return FMODRolloff.CurveGain(backend.DistanceModel, distance, referenceDistance, maxDistance, rolloff);
    }

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

        var dsps = new[] { sendEQ, send, eq };

        foreach (var dsp in dsps)
        {
            if (HasChannel && dsp.hasHandle())
                Call(channel.removeDSP(dsp), "Channel::removeDSP");
        }

        if (HasChannel)
            Call(channel.stop(), "Channel::stop");

        // System::release already freed these with the device
        if (backend.IsInitialised)
        {
            foreach (var dsp in dsps)
            {
                if (dsp.hasHandle())
                    backend.Check(dsp.release(), "DSP::release");
            }

            if (streamSound.hasHandle())
                backend.Check(streamSound.release(), "Sound::release");
        }

        disposed = true;
        channel.clearHandle();
        eq.clearHandle();
        send.clearHandle();
        sendEQ.clearHandle();
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
            FMODBackend.ToggleSign8(bytes, length);

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
            format = FMODBackend.ToFMOD(format),
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
