// AI-created

namespace vaudio_godot_mono_openal;

// Base type (Node2D/Node3D) and [Tool] are declared per-addon in VAPlayerBase.cs.
public partial class VAPlayer
{
    protected List<VAVoice> voices = [];

    // The muffling filter last passed to UpdateFilter, or null if it never was
    protected VALowPassFilter? mufflingFilter;

    // The reverb effect each voice sends to, or null for no reverb. Applied by the next UpdateFilter
    public VAReverbEffect reverbEffect;

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
        var enabled = !ReverbOnly && !VAAudioBackend.ReverbOnly;
        var changed = enabled != dryOutputEnabled;
        dryOutputEnabled = enabled;
        return changed;
    }

    // Applies the direct filter (or silence while reverb-only) and the reverb send
    protected void ApplyFilters(VAVoice voice)
    {
        var filter = mufflingFilter ?? VALowPassFilter.Full;

        // For reverb in other rooms, we send the sound's clear audio to the reverb effect, then reduce the effect's gain to make it muffled
        var send = fullReverb ? VALowPassFilter.Full : filter;
        var direct = dryOutputEnabled ? filter : VALowPassFilter.Silence;

        voice.SetFilters(direct, reverbEffect, send);
    }

    public void UpdateFilter(float gain, float gainHF, bool fullReverb = false)
    {
        VAAudioBackend.Ensure();

        mufflingFilter = new VALowPassFilter(gain, gainHF);
        this.fullReverb = fullReverb;
        UpdateDryOutput();

        foreach (var voice in voices)
            ApplyFilters(voice);
    }

    bool streamsErrorLogged = false;
    int lastPlayedStreamIndex = -1;
    static Random random = new();

    bool playRequested = false;
    float requestedStartTime;

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

    protected virtual void ConfigureVoice(VAVoice voice)
    {
    }

    // For sources that play at the listener, e.g. ambience, music, UI
    protected static void ConfigureListenerRelativeVoice(VAVoice voice)
    {
        voice.SetRelative(true);
        voice.SetPosition(Vector3.Zero);
    }

    public bool Play() => Play(0);

    /// <summary>
    /// Plays a new sound, starting <paramref name="startTime"/> seconds in. While <see cref="StreamPaused"/> is set it starts paused, and plays once unpaused
    /// </summary>
    public virtual bool Play(float startTime)
    {
        VAAudioBackend.Ensure();

        var streamIndex = PickStreamIndex();

        if (streamIndex < 0)
        {
            if (!streamsErrorLogged)
            {
                LogWarning($"Unable to play {Name} because its Streams property is not set");
                streamsErrorLogged = true;
            }

            return false;
        }

        var pickedStream = StreamAt(streamIndex);

        if (pickedStream == null)
        {
            if (!streamsErrorLogged)
            {
                LogWarning($"Unable to play {Name} because Streams[{streamIndex}] is not a valid AudioStream");
                streamsErrorLogged = true;
            }

            return false;
        }

        if (!VAAudioBackend.TryCreateVoice(pickedStream, true, out var voice))
        {
            // Buffer is still decoding in the background
            playRequested = true;
            requestedStartTime = startTime;
            return false;
        }

        playRequested = false;
        lastPlayedStreamIndex = streamIndex;

        // Matches AudioStreamRandomizer's random_pitch/random_volume_offset_db
        var pitchLow = 1 / PitchRandomness;
        var randomizedPitch = Pitch * (float)(pitchLow + random.NextDouble() * (PitchRandomness - pitchLow));
        var randomizedGain = Volume * Mathf.DbToLinear((float)(-VolumeRandomnessDb + random.NextDouble() * (2 * VolumeRandomnessDb)));

        // Set initial properties
        voice.SetGain(randomizedGain);
        voice.SetPitch(randomizedPitch);
        voice.Looping = Looping;
        ConfigureVoice(voice);

        UpdateDryOutput();
        ApplyFilters(voice);

        voices.Add(voice);

        // An initial voice starts from its offset once played
        if (startTime > 0)
            SeekVoice(voice, startTime);

        if (!_streamPaused)
            voice.Play();

        return true;
    }

    bool _streamPaused;

    /// <summary>
    /// Pauses every sound this source is playing, and resumes them from the same time when set back to false. Sounds played while paused start paused. Matches AudioStreamPlayer's stream_paused
    /// </summary>
    [Export]
    public bool StreamPaused
    {
        get => _streamPaused;
        set
        {
            if (_streamPaused == value)
                return;

            _streamPaused = value;

            foreach (var voice in voices)
                ApplyPaused(voice);
        }
    }

    protected void ApplyPaused(VAVoice voice)
    {
        var state = voice.State;

        if (_streamPaused)
        {
            if (state == VAVoiceState.Playing)
                voice.Pause();
        }
        // Not a stopped one, which would restart from the beginning
        else if (state == VAVoiceState.Paused || state == VAVoiceState.Initial)
            voice.Play();
    }

    /// <summary>
    /// Moves every sound this source is playing to <paramref name="time"/> seconds, while playing or paused. A looping sound wraps around, and a sound that doesn't loop stops if it's past the end. Matches AudioStreamPlayer's seek()
    /// </summary>
    public virtual void Seek(float time)
    {
        foreach (var voice in voices)
            SeekVoice(voice, time);
    }

    static void SeekVoice(VAVoice voice, float seconds)
    {
        var length = voice.Length;

        if (length <= 0)
            return;

        seconds = MathF.Max(0, seconds);

        if (seconds >= length)
        {
            if (!voice.Looping)
            {
                voice.Stop();
                return;
            }

            seconds %= length;
        }

        voice.PlaybackPosition = seconds;
    }

    /// <summary>
    /// How many seconds into the most recently played sound it is, or 0 if nothing is playing. Matches AudioStreamPlayer's get_playback_position()
    /// </summary>
    // The most recently played voice, or null. For tests
    internal VAVoice LastVoice => voices.Count > 0 ? voices[^1] : null;

    public float GetPlaybackTime()
    {
        if (voices.Count == 0)
            return 0;

        return voices[^1].PlaybackPosition;
    }

    public virtual void Stop() => StopPlayback();

    // Stops the sound without going through the overrides, which also cancel a waiting play
    internal void StopPlayback()
    {
        playRequested = false;

        // Release them now - a stopped looping voice never reports finished, so _Process would never clean it up
        foreach (var voice in voices)
        {
            voice.Stop();
            voice.Dispose();
        }

        voices.Clear();
    }

    // False while paused, matching AudioStreamPlayer
    public bool IsPlaying() => !_streamPaused && HasSound();

    // Whether a sound is playing or paused
    internal bool HasSound()
    {
        foreach (var voice in voices)
            if (!voice.IsFinished)
                return true;

        return false;
    }

    // A Play() waiting for its streams to decode, which isn't playing yet
    internal bool IsPlayRequested => playRequested;

    /// <summary>
    /// Whether the sound is sent to a reverb effect (grouped or the listener's). A raytraced source outside the bounds has none
    /// </summary>
    public bool HasReverb() => reverbEffect != null;

    // GDScript aliases so existing AudioStreamPlayer scripts don't break
    public bool play() => Play();
    public bool play(float start_time) => Play(start_time);
    public void stop() => Stop();
    public bool is_playing() => IsPlaying();
    public void seek(float time) => Seek(time);
    public float get_playback_position() => GetPlaybackTime();

    // Releases every voice, e.g. before the device is destroyed or when the node is freed
    protected void DisposeVoices()
    {
        foreach (var voice in voices)
            voice.Dispose();

        voices.Clear();
    }

    public virtual void OnDeviceDestroyed()
    {
        DisposeVoices();

        // The effect belongs to the old device
        reverbEffect = null;
    }
}
