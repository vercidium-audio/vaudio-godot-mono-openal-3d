using OpenALSource = global::OpenAL.managed.ALSource;
using OpenALStreamSource = global::OpenAL.managed.ALStreamSource;

namespace vaudio_godot_mono_openal;

[Tool]
[GlobalClass]
public partial class VAStreamSource : VARaytracedSource
{
    OpenALStreamSource streamSource;

    public bool IsStreamOpen => streamSource != null;

    public bool OpenStream(int format, int frequency)
    {
        CloseStream();

        if (!ALManager.Initialised)
        {
            LogWarning($"Unable to open a stream on {Name} because the ALManager has not been initialised yet. Ensure the autoload is set up correctly.");
            return false;
        }

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
        return true;
    }

    public void PushAudioData(byte[] data)
    {
        if (streamSource == null)
        {
            LogWarning($"PushAudioData called on {Name} before OpenStream (or after CloseStream)");
            return;
        }

        if (data == null || data.Length == 0)
            return;

        streamSource.EnqueueData(data, 0, data.Length);
    }

    public void CloseStream()
    {
        if (streamSource == null)
            return;

        sources.Remove(streamSource);
        streamSource.Dispose();
        streamSource = null;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        DrainUsedChunks();
    }

    void DrainUsedChunks()
    {
        while (streamSource != null && streamSource.TryGetUsedData(out _))
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
