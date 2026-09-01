using OpenALStreamSource = global::OpenAL.managed.ALStreamSource;

namespace vaudio_godot_mono_openal;

// NOTE: VAStreamSource is still OpenAL-only. Its PCM enqueue/dequeue is heavily OpenAL-shaped
// (ALStreamSource.EnqueueData / TryGetUsedData), so it creates an OpenAL stream voice directly.
// It is still wrapped in an OpenALSourceHandle and added to the base `sources` list, so it rides
// all the base AudioSource per-frame updates (position, distance, filter/reverb) exactly as before.
// The FMOD backend will need an FMOD_OPENUSER equivalent before this node can drop the direct
// OpenAL dependency (plan 3.6).
[Tool]
[GlobalClass]
public partial class VAStreamSource : VARaytracedSource
{
    OpenALStreamSource streamSource;
    IAudioSourceHandle streamHandle;

    public bool IsStreamOpen => streamSource != null;

    public bool OpenStream(int format, int frequency)
    {
        CloseStream();

        if (!AudioManager.Initialised)
        {
            LogWarning($"Unable to open a stream on {Name} because the audio backend has not been initialised yet. Ensure the autoload is set up correctly.");
            return false;
        }

        var sourceID = AL.GenSource();

        if (sourceID == 0)
        {
            LogWarning($"Failed to create a stream source for {Name} - likely too many sources have been created");
            return false;
        }

        streamSource = new OpenALStreamSource(sourceID, format, frequency);
        streamSource.SetGain(Volume);
        streamSource.SetPitch(Pitch);

        streamHandle = new OpenALSourceHandle(streamSource);
        ConfigureSource(streamHandle);

        var directFilter = AudioManager.ReverbOnly ? silenceFilter :
            (filter != null ? new AudioFilter(filter.Gain, filter.GainHF) : fullFilter);

        streamHandle.ApplyReverb(effect, directFilter, fullFilter);

        sources.Add(streamHandle);
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

        if (streamHandle != null)
        {
            sources.Remove(streamHandle);
            streamHandle.Dispose();
            streamHandle = null;
        }

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
        // base disposes streamHandle via `sources`
        base.OnDeviceDestroyed();
        streamSource = null;
        streamHandle = null;
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
