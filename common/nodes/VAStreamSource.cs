// AI-created

namespace vaudio_godot_mono_openal;

[Tool]
[GlobalClass]
public partial class VAStreamSource : VARaytracedSource
{
    VAStream stream;

    // Set once the emitter is first ready to play and stays true, so a RaytraceOnce stream keeps accepting data after its emitter leaves the world
    bool streamReady;

    bool streamOpen;
    VAPcmFormat streamFormat;
    int streamFrequency;

    // Set when the bounds stopped the sound, or the stream was opened outside them, so it only restarts once inside if the bounds were the reason
    bool stoppedByBounds;

    public bool IsStreamOpen => streamOpen;

    // If outside the world bounds the stream opens without a sound, then starts once inside
    public bool OpenStream(VAPcmFormat format, int frequency)
    {
        VAAudioBackend.Ensure();

        CloseStream();

        streamFormat = format;
        streamFrequency = frequency;
        stoppedByBounds = IsSilencedByBounds();

        if (!stoppedByBounds && !StartSound())
            return false;

        streamOpen = true;
        streamReady = false;
        return true;
    }

    // The stream's queued audio lives in its source, so a fresh one starts from the next push
    bool StartSound()
    {
        // The backend logs why it failed
        if (!VAAudioBackend.TryCreateStream(streamFormat, streamFrequency, out var newStream))
            return false;

        var voice = newStream.Voice;
        voice.SetGain(Volume);
        voice.SetPitch(Pitch);
        ConfigureVoice(voice);

        UpdateDryOutput();
        ApplyFilters(voice);

        voices.Add(voice);
        stream = newStream;

        // The stream voice starts playing as soon as it's created
        ApplyPaused(voice);
        return true;
    }

    public override void Seek(float time)
    {
        LogWarning($"{Name}: A VAStreamSource can't seek");
    }

    void StopSound()
    {
        if (stream == null)
            return;

        voices.Remove(stream.Voice);

        if (!stream.Voice.IsDisposed)
            stream.Voice.Dispose();

        stream = null;
    }

    void ApplyBoundsRules()
    {
        if (!streamOpen)
            return;

        if (IsSilencedByBounds())
        {
            if (stream == null)
                return;

            StopSound();
            stoppedByBounds = true;
            return;
        }

        if (!stoppedByBounds)
            return;

        // Audio queued before it left would play late, so the new sound starts from the next push
        stoppedByBounds = false;

        if (!StartSound())
        {
            LogWarning($"{Name}: The stream failed to create its sound, so it was closed");
            CloseStream();
        }
    }

    public void PushAudioData(byte[] data)
    {
        if (!streamOpen)
        {
            LogWarning($"{Name}: PushAudioData failed as the stream never opened");
            return;
        }

        // Nothing plays it while the bounds have stopped it, and audio queued while paused would play late. A destroyed device releases the source without going through CloseStream
        if (data == null || data.Length == 0 || stoppedByBounds || StreamPaused || stream == null || stream.Voice.IsDisposed)
            return;

        // Data that arrives before the muffling and reverb results is dropped, so the stream never plays unmuffled or without reverb
        if (!streamReady)
        {
            if (!IsReadyToPlay)
                return;

            streamReady = true;
        }

        stream.Enqueue(data, 0, data.Length);
    }

    public void CloseStream()
    {
        StopSound();
        streamOpen = false;
        stoppedByBounds = false;
    }

    // Closes the stream too, else IsStreamOpen stays true with no source playing it
    public override void Stop()
    {
        CloseStream();
        base.Stop();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Engine.IsEditorHint())
            return;

        ApplyBoundsRules();

        // Also set streamReady here in case nothing is pushed this frame. A RaytraceOnce emitter removes itself from the world later this frame, after which IsReadyToPlay is false and PushAudioData would drop everything
        if (streamOpen && !streamReady && IsReadyToPlay)
            streamReady = true;
    }

    public override void OnDeviceDestroyed()
    {
        // base already disposes the stream's voice via `voices`, which closes the stream
        base.OnDeviceDestroyed();
        stream = null;
        streamOpen = false;
        stoppedByBounds = false;
    }

    static readonly StringName[] hiddenProperties =
    [
        PropertyName.Streams,
        PropertyName.Looping,
        PropertyName.Autoplay,
        PropertyName.PitchRandomness,
        PropertyName.VolumeRandomnessDb,
        PropertyName.PlaybackNoRepeat,
    ];

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        base._ValidateProperty(property);

        if (Array.IndexOf(hiddenProperties, property["name"].AsStringName()) >= 0)
            property["usage"] = (int)PropertyUsageFlags.None;
    }
}
