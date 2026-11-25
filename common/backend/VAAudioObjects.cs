namespace vaudio_godot_mono_openal;

/// <summary>
/// One playing instance of a sound inside the audio backend (an OpenAL source, an FMOD channel, ...). Each player node owns one, or a few while one-shots overlap
/// </summary>
public abstract class VAVoice : IDisposable
{
    public abstract bool IsDisposed { get; }

    public abstract VAVoiceState State { get; }

    /// <summary>Stopped and not looping, so it can be disposed</summary>
    public abstract bool IsFinished { get; }

    public abstract void Play();
    public abstract void Pause();
    public abstract void Stop();

    /// <summary>The sound's length in seconds, or 0 if unknown (e.g. a stream)</summary>
    public abstract float Length { get; }

    /// <summary>Seconds into the sound</summary>
    public abstract float PlaybackPosition { get; set; }

    public abstract bool Looping { get; set; }

    public abstract void SetGain(float gain);
    public abstract void SetPitch(float pitch);

    /// <summary>Whether the position is relative to the listener rather than the world</summary>
    public abstract void SetRelative(bool relative);

    public abstract void SetSpatialised(bool spatialised);
    public abstract void SetPosition(Vector3 position);
    public abstract void SetVelocity(Vector3 velocity);
    public abstract void SetReferenceDistance(float distance);
    public abstract void SetMaxDistance(float distance);
    public abstract void SetRolloff(float rolloff);

    /// <summary>Sets the direct path's low pass, and sends the voice through the send low pass into reverbBus (null for no reverb)</summary>
    public abstract void SetFilters(VALowPass direct, VAReverbBus reverbBus, VALowPass send);

    public abstract void Dispose();
}

/// <summary>
/// Raw PCM pushed into the backend and played by <see cref="Voice"/>, e.g. for VAStreamSource. Disposing the voice closes the stream
/// </summary>
public abstract class VAStream
{
    public abstract VAVoice Voice { get; }

    /// <summary>Queues audio to play after what's already queued. The array must not be changed until it's been played</summary>
    public abstract void Enqueue(byte[] data, int offset, int length);

    /// <summary>Releases the chunks that have finished playing</summary>
    public abstract void ReleasePlayedChunks();
}

/// <summary>
/// Decoded audio the backend can play
/// </summary>
public abstract class VASound : IDisposable
{
    /// <summary>The length in seconds</summary>
    public abstract float Length { get; }

    /// <summary>Fails when the backend is out of voices</summary>
    public abstract bool TryCreateVoice(bool spatialised, out VAVoice voice);

    public abstract void Dispose();
}

/// <summary>
/// A reverb effect that voices send into. Each VAWorld owns one for the listener's reverb, plus one per grouped EAX
/// </summary>
public abstract class VAReverbBus : IDisposable
{
    /// <summary>False if the backend failed to create it</summary>
    public abstract bool IsValid { get; }

    /// <summary>Applies the raytraced reverb. gain is the bus's output volume, and the pans point where the reflections and late reverb come from, relative to the listener</summary>
    public abstract void SetParams(vaudio.EAXReverb eax, float gain, Vector3 reflectionsPan, Vector3 lateReverbPan);

    public abstract void Dispose();
}

/// <summary>
/// A microphone or other input device
/// </summary>
public abstract class VACaptureDevice
{
    public abstract void Start();
    public abstract void Stop();

    /// <summary>Reads what's been captured since the last poll into the onData callback</summary>
    public abstract void Poll();

    public abstract void Close();
}
