using OpenALSource = global::OpenAL.managed.ALSource;
using OpenALStreamSource = global::OpenAL.managed.ALStreamSource;

namespace vaudio_godot_mono_openal;

[Tool]
[GlobalClass]
public partial class VAStreamSource : VARaytracedSource
{
    OpenALStreamSource streamSource;

    // Latched once the emitter is first ready to play, so a RaytraceOnce stream keeps accepting data after its emitter leaves the world
    bool streamReady;

    bool streamOpen;
    int streamFormat;
    int streamFrequency;

    // Set when the bounds stopped the sound, or the stream was opened outside them, so it only restarts once inside if the bounds were the reason
    bool stoppedByBounds;

    public bool IsStreamOpen => streamOpen;

    // Outside the bounds (while they silence it) the stream opens without a sound, which starts once inside
    public bool OpenStream(int format, int frequency)
    {
        ALManager.Ensure();

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
        var sourceID = AL.GenSource();

        if (sourceID == 0)
        {
            LogWarning($"Failed to create a stream source for {Name} - likely too many sources have been created");
            return false;
        }

        var source = new OpenALStreamSource(sourceID, streamFormat, streamFrequency);
        source.SetGain(Volume);
        source.SetPitch(Pitch);
        ConfigureSource(source);

        var directFilter = ALManager.ReverbOnly ? silenceFilter : filter;
        source.SetFilter(effect, directFilter, fullFilter);

        sources.Add(source);
        streamSource = source;
        return true;
    }

    void StopSound()
    {
        if (streamSource == null)
            return;

        sources.Remove(streamSource);

        if (!streamSource.IsDisposed())
            streamSource.Dispose();

        streamSource = null;
    }

    void ApplyBoundsRules()
    {
        if (!streamOpen)
            return;

        if (IsSilencedByBounds())
        {
            if (streamSource == null)
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
            LogWarning($"PushAudioData called on {Name} before OpenStream (or after CloseStream)");
            return;
        }

        // Nothing plays it while the bounds have stopped it. A destroyed device releases the source without going through CloseStream
        if (data == null || data.Length == 0 || stoppedByBounds || streamSource == null || streamSource.IsDisposed())
            return;

        // Data that arrives before the muffling and reverb results is dropped, so the stream never plays unmuffled or without reverb
        if (!streamReady)
        {
            if (!IsReadyToPlay)
                return;

            streamReady = true;
        }

        streamSource.EnqueueData(data, 0, data.Length);
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

        // Latched here too, since a RaytraceOnce emitter leaves the world later this frame
        if (streamOpen && !streamReady && IsReadyToPlay)
            streamReady = true;

        DrainUsedChunks();
    }

    void DrainUsedChunks()
    {
        while (streamSource != null && !streamSource.IsDisposed() && streamSource.TryGetUsedData(out _))
        {
        }
    }

    public override void OnDeviceDestroyed()
    {
        // base already disposes streamSource via `sources`, which closes the stream
        base.OnDeviceDestroyed();
        streamSource = null;
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
