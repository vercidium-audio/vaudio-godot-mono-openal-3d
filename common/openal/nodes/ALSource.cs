using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

// Base type (Node2D/Node3D) and [Tool] are declared per-addon in ALSourceBase.cs.
public partial class ALSource
{
    protected List<OpenALSource> sources = [];
    public ALFilter filter;
    public ALReverbEffect effect;

    static ALFilter _silenceFilter;
    static ALFilter _fullFilter;
    static bool nativeLoadErrorLogged = false;

    public static ALFilter silenceFilter => _silenceFilter ??= CreateFilterSafe(0, 0);
    public static ALFilter fullFilter => _fullFilter ??= CreateFilterSafe(1, 1);

    static ALFilter CreateFilterSafe(float gain, float gainHF)
    {
        try
        {
            return new ALFilter(gain, gainHF);
        }
        catch (Exception ex)
        {
            if (!nativeLoadErrorLogged)
            {
                LogError($"Failed to initialise OpenAL ({ex.Message}). Audio playback will not work until this is fixed.");
                nativeLoadErrorLogged = true;
            }

            return null;
        }
    }

    // The fullReverb last passed to UpdateFilter
    bool fullReverb = true;

    bool dryOutputEnabled = true;

    /// <summary>
    /// Whether this source's direct sound is heard, i.e. neither this source's <see cref="ReverbOnly"/> nor <see cref="VAWorld.ReverbOnly"/> is set. Updated each frame
    /// </summary>
    public bool IsDryOutputEnabled() => dryOutputEnabled;

    // Recalculates dryOutputEnabled, returning whether it changed
    protected bool UpdateDryOutput()
    {
        var enabled = !ReverbOnly && !ALManager.ReverbOnly;
        var changed = enabled != dryOutputEnabled;
        dryOutputEnabled = enabled;
        return changed;
    }

    // Applies the direct filter (or the silence filter while reverb-only) and the reverb send
    protected void ApplyFilters(OpenALSource source)
    {
        // For reverb in other rooms, we send the sound's clear audio to the reverb effect, then reduce the reverb effect's gain to make it muffled
        var reverbFilter = fullReverb ? fullFilter : filter;
        var directFilter = dryOutputEnabled ? filter : silenceFilter;

        source.SetFilter(effect, directFilter, reverbFilter);
    }

    public void UpdateFilter(float gain, float gainHF, bool fullReverb = false)
    {
        ALManager.Ensure();

        if (filter == null)
            filter = new(gain, gainHF);
        else
            filter.SetGain(gain, gainHF);

        this.fullReverb = fullReverb;
        UpdateDryOutput();

        foreach (var s in sources)
            ApplyFilters(s);
    }

    bool streamsErrorLogged = false;
    int lastPlayedStreamIndex = -1;
    static Random random = new();

    bool playRequested = false;

    int PickStreamIndex()
    {
        if (_streams.Count == 0)
            return -1;

        if (_streams.Count == 1)
            return 0;

        var index = random.Next(_streams.Count);

        if (PlaybackNoRepeat && index == lastPlayedStreamIndex)
            index = (index + 1) % _streams.Count;

        return index;
    }

    protected virtual void ConfigureSource(OpenALSource source)
    {
    }

    public virtual bool Play()
    {
        ALManager.Ensure();

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

        var pickedStream = StreamAt(streamIndex);

        if (pickedStream == null)
        {
            if (!streamsErrorLogged)
            {
                LogWarning($"Unable to play the ALSource {Name} because Streams[{streamIndex}] is not a valid AudioStream");
                streamsErrorLogged = true;
            }

            return false;
        }

        if (!ALManager.TryCreateSource(pickedStream, true, out var source))
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

        UpdateDryOutput();
        ApplyFilters(source);

        source.Play();
        sources.Add(source);
        return true;
    }

    public virtual void Stop() => StopPlayback();

    // Stops the sound without going through the overrides, which also cancel a waiting play
    internal void StopPlayback()
    {
        playRequested = false;

        // Release them now - a stopped looping source never reports finished, so _Process would never clean it up
        foreach (var s in sources)
        {
            s.Stop();
            s.Dispose();
        }

        sources.Clear();
    }

    public bool IsPlaying()
    {
        foreach (var s in sources)
            if (!s.Finished())
                return true;

        return false;
    }

    // A Play() waiting for its streams to decode, which isn't playing yet
    internal bool IsPlayRequested => playRequested;

    /// <summary>
    /// Whether the sound is sent to a reverb effect (grouped or the listener's). A raytraced source outside the bounds has none
    /// </summary>
    public bool HasReverb() => effect != null;

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
