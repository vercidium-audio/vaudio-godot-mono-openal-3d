using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

// Base type (Node2D/Node3D) and [Tool] are declared per-addon in ALSourceBase.cs.
public partial class ALSource
{
    protected List<OpenALSource> sources = [];
    public ALFilter filter;
    public ALReverbEffect effect;

    public static ALFilter silenceFilter = new(0, 0);
    public static ALFilter fullFilter = new(1, 1);

    public void UpdateFilter(float gain, float gainHF, bool fullReverb = false)
    {
        if (!ALManager.Initialised)
            return;

        if (filter == null)
            filter = new(gain, gainHF);
        else
            filter.SetGain(gain, gainHF);


        // For reverb in other rooms, we send the sound's clear audio to the reverb effect,
        //  then reduce the reverb effect's gain to make it muffled
        var reverbFilter = fullReverb ? fullFilter : filter;

        var directFilter = ALManager.ReverbOnly ? silenceFilter : filter;

        foreach (var s in sources)
            s.SetFilter(effect, directFilter, reverbFilter);
    }

    bool streamsErrorLogged = false;
    bool alWarningLogged = false;
    int lastPlayedStreamIndex = -1;
    static Random random = new();

    bool playRequested = false;

    int PickStreamIndex()
    {
        if (_streams.Length == 0)
            return -1;

        if (_streams.Length == 1)
            return 0;

        var index = random.Next(_streams.Length);

        if (PlaybackNoRepeat && index == lastPlayedStreamIndex)
            index = (index + 1) % _streams.Length;

        return index;
    }

    protected virtual void ConfigureSource(OpenALSource source)
    {
    }

    public virtual bool Play()
    {
        var streamIndex = PickStreamIndex();

        if (streamIndex < 0)
        {
            if (!streamsErrorLogged)
            {
                LogWarning($"Unable to play the ALSource {Name} because its Streams property is not set");
                streamsErrorLogged = true;
            }

            return false;
        }

        if (!ALManager.Initialised)
        {
            if (!alWarningLogged)
            {
                LogWarning($"Unable to play the ALSource {Name} because the ALManager has not been initialised yet. Ensure the autoload is set up correctly.");
                alWarningLogged = true;
            }

            return false;
        }

        if (!ALManager.TryCreateSource(_streams[streamIndex], true, out var source))
        {
            // Buffer is still decoding in the background
            playRequested = true;
            return false;
        }

        playRequested = false;
        lastPlayedStreamIndex = streamIndex;

        // Matches AudioStreamRandomizer's random_pitch/random_volume_offset_db
        var pitchLow = 1 / PitchRandomness;
        var randomizedPitch = Pitch * (float)(pitchLow + random.NextDouble() * (PitchRandomness - pitchLow));
        var randomizedGain = Volume * Mathf.DbToLinear((float)(-VolumeRandomnessDb + random.NextDouble() * (2 * VolumeRandomnessDb)));

        // Set initial properties
        source.SetGain(randomizedGain);
        source.SetPitch(randomizedPitch);
        source.SetLooping(Looping);
        ConfigureSource(source);

        var directFilter = ALManager.ReverbOnly ? silenceFilter : filter;

        // For reverb in other rooms, we send the sound's clear audio to the reverb effect,
        //  then reduce the reverb effect's gain to make it muffled
        var fullReverb = true;
        var reverbFilter = fullReverb ? fullFilter : filter;

        source.SetFilter(effect, directFilter, reverbFilter);

        source.Play();
        sources.Add(source);
        return true;
    }

    public void Stop()
    {
        playRequested = false;

        foreach (var s in sources)
            s.Stop();
    }

    public bool IsPlaying()
    {
        foreach (var s in sources)
            if (!s.Finished())
                return false;

        return true;
    }

    // GDScript aliases so existing AudioStreamPlayer scripts don't break
    public bool play() => Play();
    public void stop() => Stop();
    public bool is_playing() => IsPlaying();

    public virtual void OnDeviceDestroyed()
    {
        foreach (var s in sources)
            s.Dispose();

        sources.Clear();

        // Must delete the filter after deleting the sources
        filter?.Delete();
        filter = null;
    }
}
