namespace vaudio_godot_mono_openal;

/// <summary>
/// OpenAL Soft implementation of <see cref="IAudioBackend"/>. Owns the whole OpenAL device/context lifecycle, the
/// per-frame pump, the decoded-buffer cache, the device-destroyed/recreated callback lists and the listener/global
/// property state. Nothing outside <c>openal/</c> touches OpenAL directly anymore - shared code goes through
/// <see cref="AudioManager.Backend"/>.
///
/// There is only ever one device/context/listener, so the listener and global-property setters talk to <c>AL.*</c>
/// directly.
/// </summary>
public unsafe class OpenALBackend : IAudioBackend
{
    // --- AL resources ---

    ALDevice alDevice;
    ALContext alContext;

    // Decoded-stream cache (one OpenAL buffer per AudioStream)
    readonly Dictionary<AudioStream, OpenALBufferHandle> decodedStreams = [];

    // Device destroyed/recreated callbacks (e.g. reverb-effect cleanup/recreation)
    readonly HashSet<Action> onDeviceDestroyedCallbacks = [];
    readonly HashSet<Action> onDeviceRecreatedCallbacks = [];

    // --- Settings read from ProjectSettings ---

    const string DefaultDeviceLabel = "System Default";

    string outputDeviceName;
    int maximumAuxiliarySends;
    int sampleRate;
    bool hrtfEnabled;
    int maximumMonoSources;
    int maximumStereoSources;

    // --- Global property state (applied on device (re)creation too) ---

    float masterVolume = 1;
    AudioDistanceModel distanceModel = AudioDistanceModel.InverseDistance;
    float metersPerUnit = 1;
    float speedOfSound = 343;
    bool reverbOnly;

    public bool Initialised => alDevice != null;

    // --- Lifecycle ---

    public void Ensure()
    {
        if (Initialised || Engine.IsEditorHint())
            return;

        // Log to both - in case we're launched from vs2026 or from the Godot Editor
        OpenAL.Logger.Log = (message) =>
        {
            Console.WriteLine(message);
            GD.Print(message);
        };
        OpenAL.Logger.Error = (message) =>
        {
            Console.Error.WriteLine(message);
            GD.PushError(message);
        };

        CreateDeviceAndContext();
    }

    public void Update()
    {
        if (Engine.IsEditorHint() || !Initialised)
            return;

        alContext.Process();
    }

    void ReadSettingsFromProjectSettings()
    {
        var deviceNameSetting = ProjectSettings.GetSetting("audio/vaudio/output_device").AsString();
        outputDeviceName = deviceNameSetting == DefaultDeviceLabel ? "" : deviceNameSetting;

        maximumAuxiliarySends = Math.Max(1, ProjectSettings.GetSetting("audio/vaudio/max_reverb_sends").AsInt32());
        sampleRate = ProjectSettings.GetSetting("audio/vaudio/sample_rate").AsInt32();
        hrtfEnabled = ProjectSettings.GetSetting("audio/vaudio/hrtf_enabled").AsBool();
        maximumMonoSources = ProjectSettings.GetSetting("audio/vaudio/max_mono_sources").AsInt32();
        maximumStereoSources = ProjectSettings.GetSetting("audio/vaudio/max_stereo_sources").AsInt32();
    }

    void CreateDeviceAndContext()
    {
        // Shouldn't be initialising in the editor
        Debug.Assert(!Engine.IsEditorHint());
        Debug.Assert(alContext == null);
        Debug.Assert(alDevice == null);

        ReadSettingsFromProjectSettings();

        alDevice = new(string.IsNullOrEmpty(outputDeviceName) ? null : outputDeviceName);

        var settings = new ALContextSettings()
        {
            HRTFEnabled = hrtfEnabled,
            HRTFID = 0,
            SampleRate = sampleRate,
            MaximumAuxiliarySends = maximumAuxiliarySends,
            MaximumMonoSources = maximumMonoSources,
            MaximumStereoSources = maximumStereoSources,
            LogWarning = LogWarning,
        };

        alContext = new(alDevice, settings);

        // Re-apply the global properties to the fresh context
        ApplyMetersPerUnit();
        ApplySpeedOfSound();
        ApplyMasterVolume();
        ApplyDistanceModel();
    }

    void RecreateDevice()
    {
        // Don't create OpenAL devices when changing properties in the editor
        if (!Initialised)
            return;

        ReadSettingsFromProjectSettings();

        var attribs = alContext.GetAttribs(new()
        {
            HRTFEnabled = hrtfEnabled,
            HRTFID = 0,
            SampleRate = sampleRate,
            MaximumAuxiliarySends = maximumAuxiliarySends,
            MaximumMonoSources = maximumMonoSources,
            MaximumStereoSources = maximumStereoSources,
        });

        // Try a reopen first. If it fails, destroy and recreate the device
        if (alDevice.Reopen(string.IsNullOrEmpty(outputDeviceName) ? null : outputDeviceName, attribs))
            return;

        CancelLoadingAndDestroy();
        CreateDeviceAndContext();

        // Invoke device recreated callbacks (e.g. for recreating reverb effects)
        foreach (var callback in onDeviceRecreatedCallbacks)
            callback.Invoke();
    }

    void DestroyAllAudioSources(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is AudioSource source)
                source.OnDeviceDestroyed();

            DestroyAllAudioSources(child);
        }
    }

    void DestroyAll()
    {
        if (alDevice == null || alContext == null)
        {
            Debug.Assert(false);
            return;
        }

        DestroyAllAudioSources(((SceneTree)Engine.GetMainLoop()).Root);

        // Invoke device destroyed callbacks (e.g. for cleaning up reverb effects)
        foreach (var callback in onDeviceDestroyedCallbacks)
            callback.Invoke();

        AL.MakeContextCurrent(IntPtr.Zero);
        alContext.Destroy();
        alContext = null;

        alDevice.Close();
        alDevice = null;
    }

    void CancelLoadingAndDestroy()
    {
        // Tell the background sound-loading threads to stop loading
        AudioBuffer.CancelLoadingSounds = true;

        // Wait for all decode + upload tasks to finish
        foreach (var handle in decodedStreams.Values)
            handle.WaitForLoad();

        decodedStreams.Clear();
        AudioBuffer.CancelLoadingSounds = false;

        DestroyAll();
    }

    // --- Listener (single listener, single context - straight to AL) ---

    public void SetListenerPosition(Vector3 p) => AL.Listenerfv(AL.AL_POSITION, [p.X, p.Y, p.Z]);

    public void SetListenerVelocity(Vector3 v) => AL.Listenerfv(AL.AL_VELOCITY, [v.X, v.Y, v.Z]);

    public void SetListenerOrientation(Vector3 forward, Vector3 up) =>
        AL.Listenerfv(AL.AL_ORIENTATION, [forward.X, forward.Y, forward.Z, up.X, up.Y, up.Z]);

    // --- Global properties ---

    public void SetMasterVolume(float volume)
    {
        masterVolume = MathF.Max(0, volume);
        ApplyMasterVolume();
    }

    public void SetMetersPerUnit(float value)
    {
        metersPerUnit = MathF.Max(0, value);
        ApplyMetersPerUnit();
    }

    public void SetSpeedOfSound(float value)
    {
        speedOfSound = MathF.Max(0, value);
        ApplySpeedOfSound();
    }

    public void SetDistanceModel(AudioDistanceModel model)
    {
        distanceModel = model;
        ApplyDistanceModel();
    }

    void ApplyMasterVolume() => AL.Listenerf(AL.AL_GAIN, masterVolume);
    void ApplyMetersPerUnit() => AL.Listenerf(AL.AL_METERS_PER_UNIT, metersPerUnit);
    void ApplySpeedOfSound() => AL.SpeedOfSound(speedOfSound);
    void ApplyDistanceModel() => AL.DistanceModel((int)ToALDistanceModel(distanceModel));

    public bool ReverbOnly => reverbOnly;
    public void SetReverbOnly(bool value) => reverbOnly = value;

    static ALDistanceModel ToALDistanceModel(AudioDistanceModel model) => model switch
    {
        AudioDistanceModel.None => ALDistanceModel.None,
        AudioDistanceModel.InverseDistance => ALDistanceModel.InverseDistance,
        AudioDistanceModel.InverseDistanceClamped => ALDistanceModel.InverseDistanceClamped,
        AudioDistanceModel.LinearDistance => ALDistanceModel.LinearDistance,
        AudioDistanceModel.LinearDistanceClamped => ALDistanceModel.LinearDistanceClamped,
        AudioDistanceModel.ExponentDistance => ALDistanceModel.ExponentDistance,
        AudioDistanceModel.ExponentDistanceClamped => ALDistanceModel.ExponentDistanceClamped,
        _ => ALDistanceModel.InverseDistance,
    };

    // --- Buffers / voices / reverb / filters ---

    public IAudioBuffer GetOrCreateBuffer(AudioStream stream)
    {
        if (decodedStreams.TryGetValue(stream, out var handle))
            return handle;

        handle = new OpenALBufferHandle(new AudioBuffer(stream), alContext);
        decodedStreams[stream] = handle;
        return handle;
    }

    public IAudioReverbSlot CreateReverbSlot() => new OpenALReverbSlot();

    public IAudioFilterHandle CreateFilter(float gain, float gainHF) => new OpenALFilterHandle(gain, gainHF);

    // --- Device callbacks ---

    public void RegisterDeviceDestroyedCallback(Action callback)
    {
        if (callback == null)
            throw new ArgumentException("callback cannot be null");

        onDeviceDestroyedCallbacks.Add(callback);
    }

    public void UnregisterDeviceDestroyedCallback(Action callback)
    {
        if (callback == null)
            throw new ArgumentException("callback cannot be null");

        onDeviceDestroyedCallbacks.Remove(callback);
    }

    public void RegisterDeviceRecreatedCallback(Action callback)
    {
        if (callback == null)
            throw new ArgumentException("callback cannot be null");

        onDeviceRecreatedCallbacks.Add(callback);
    }

    public void UnregisterDeviceRecreatedCallback(Action callback)
    {
        if (callback == null)
            throw new ArgumentException("callback cannot be null");

        onDeviceRecreatedCallbacks.Remove(callback);
    }

    // --- Output devices ---

    public string[] GetOutputDevices() => [.. AL.GetStringList(IntPtr.Zero, AL.ALC_ALL_DEVICES_SPECIFIER)];

    public void SetOutputDevice(string deviceName)
    {
        outputDeviceName = deviceName;
        RecreateDevice();
    }
}
