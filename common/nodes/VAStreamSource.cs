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

    public bool IsStreamOpen => streamSource != null;

    public bool OpenStream(int format, int frequency)
    {
        ALManager.Ensure();
        
        CloseStream();

        var sourceID = AL.GenSource();

        if (sourceID == 0)
        {
            LogWarning($"Failed to create a stream source for {Name} - likely too many sources have been created");
            return false;
        }

        var source = new OpenALStreamSource(sourceID, format, frequency);
        source.SetGain(Volume);
        source.SetPitch(Pitch);
        ConfigureSource(source);

        var directFilter = ALManager.ReverbOnly ? silenceFilter : filter;
        source.SetFilter(effect, directFilter, fullFilter);

        sources.Add(source);
        streamSource = source;
        streamReady = false;
        return true;
    }

    public void PushAudioData(byte[] data)
    {
        if (streamSource == null)
        {
            LogWarning($"PushAudioData called on {Name} before OpenStream (or after CloseStream)");
            return;
        }

        // Stop() or finishing releases the source without going through CloseStream
        if (data == null || data.Length == 0 || streamSource.IsDisposed())
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
        if (streamSource == null)
            return;

        sources.Remove(streamSource);

        if (!streamSource.IsDisposed())
            streamSource.Dispose();

        streamSource = null;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        // Latched here too, since a RaytraceOnce emitter leaves the world later this frame
        if (streamSource != null && !streamReady && IsReadyToPlay)
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
        // base already disposes streamSource via `sources`
        base.OnDeviceDestroyed();
        streamSource = null;
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
