namespace vaudio_godot_mono_openal;

/// <summary>
/// An audio library that plays vaudio's sounds (OpenAL, FMOD, ...). Exactly one is used per game, chosen by the audio/vaudio/backend project setting. Implementations register themselves with <see cref="VAAudioBackendRegistry"/>. The global mix settings are cached here: setting one stores it, and applies it straight away when initialised
/// </summary>
public abstract class VAAudioBackendBase
{
    /// <summary>The audio/vaudio/backend value that selects this backend</summary>
    public abstract string Name { get; }

    public abstract bool IsInitialised { get; }

    /// <summary>Opens the output device. Never called in the editor</summary>
    public abstract void Initialise();

    /// <summary>Called once per frame while initialised</summary>
    public abstract void Update();

    /// <summary>Raises <see cref="DeviceDestroying"/>, then closes the output device</summary>
    public abstract void Shutdown();

    int _errorCount;

    /// <summary>How many backend errors have been logged since the game started, so tests can check a run hit none</summary>
    public int ErrorCount => System.Threading.Volatile.Read(ref _errorCount);

    /// <summary>Logs a failed backend call and counts it in <see cref="ErrorCount"/></summary>
    public void LogError(string message)
    {
        System.Threading.Interlocked.Increment(ref _errorCount);
        Logger.LogError(message);
    }

    /// <summary>Raised before the device is destroyed (e.g. when switching output devices), after which every voice, sound and reverb bus is invalid</summary>
    public event Action DeviceDestroying;

    /// <summary>Raised once the device has been recreated</summary>
    public event Action DeviceRecreated;

    protected void RaiseDeviceDestroying() => DeviceDestroying?.Invoke();
    protected void RaiseDeviceRecreated() => DeviceRecreated?.Invoke();

    float _masterVolume = 1;
    VADistanceModel _distanceModel = VADistanceModel.InverseClamped;
    float _metersPerUnit = 1;
    float _speedOfSound = 343;
    float _dopplerScale = 1;

    public float MasterVolume
    {
        get => _masterVolume;
        set => UpdateProperty(ref _masterVolume, MathF.Max(0, value), ApplyMasterVolume);
    }

    public VADistanceModel DistanceModel
    {
        get => _distanceModel;
        set => UpdateProperty(ref _distanceModel, value, ApplyDistanceModel);
    }

    public float MetersPerUnit
    {
        get => _metersPerUnit;
        set => UpdateProperty(ref _metersPerUnit, MathF.Max(0, value), ApplyMetersPerUnit);
    }

    public float SpeedOfSound
    {
        get => _speedOfSound;
        set => UpdateProperty(ref _speedOfSound, MathF.Max(0, value), ApplySpeedOfSound);
    }

    public float DopplerScale
    {
        get => _dopplerScale;
        set => UpdateProperty(ref _dopplerScale, MathF.Max(0, value), ApplyDopplerScale);
    }

    Vector3 _listenerPosition;
    Vector3 _listenerVelocity;
    Vector3 _listenerForward = new(0, 0, -1);
    Vector3 _listenerUp = Vector3.Up;

    public void SetListenerPosition(Vector3 position) => UpdateProperty(ref _listenerPosition, position, ApplyListenerPosition);
    public void SetListenerVelocity(Vector3 velocity) => UpdateProperty(ref _listenerVelocity, velocity, ApplyListenerVelocity);

    public void SetListenerOrientation(Vector3 forward, Vector3 up)
    {
        if (forward == _listenerForward && up == _listenerUp)
            return;

        _listenerForward = forward;
        _listenerUp = up;

        if (IsInitialised)
            ApplyListenerOrientation(forward, up);
    }

    // Pushes every cached setting to a newly created device
    protected void ApplyAllSettings()
    {
        ApplyMasterVolume(_masterVolume);
        ApplyDistanceModel(_distanceModel);
        ApplyMetersPerUnit(_metersPerUnit);
        ApplySpeedOfSound(_speedOfSound);
        ApplyDopplerScale(_dopplerScale);
        ApplyListenerPosition(_listenerPosition);
        ApplyListenerVelocity(_listenerVelocity);
        ApplyListenerOrientation(_listenerForward, _listenerUp);
    }

    protected abstract void ApplyMasterVolume(float volume);
    protected abstract void ApplyDistanceModel(VADistanceModel model);
    protected abstract void ApplyMetersPerUnit(float metersPerUnit);
    protected abstract void ApplySpeedOfSound(float speedOfSound);
    protected abstract void ApplyDopplerScale(float scale);
    protected abstract void ApplyListenerPosition(Vector3 position);
    protected abstract void ApplyListenerVelocity(Vector3 velocity);
    protected abstract void ApplyListenerOrientation(Vector3 forward, Vector3 up);

    void UpdateProperty<T>(ref T field, T value, Action<T> apply) where T : struct
    {
        if (field.Equals(value))
            return;

        field = value;

        if (IsInitialised)
            apply(value);
    }

    public abstract string[] GetOutputDevices();

    /// <summary>Switches to the named output device, or the system default when empty</summary>
    public abstract void SetOutputDevice(string name);

    public abstract string[] GetCaptureDevices();

    /// <summary>Refreshes the dropdowns of this backend's own project settings. Called in the editor</summary>
    public abstract void RefreshSettingHints();

    /// <summary>Uploads decoded 16-bit PCM. Called from a worker thread. Returns null if the device isn't available</summary>
    public abstract VASound CreateSound(short[] pcm16, int channels, int sampleRate);

    /// <summary>Creates a stream that plays audio pushed into it. Its voice starts playing straight away. Fails when out of voices</summary>
    public abstract bool TryCreateStream(VAPcmFormat format, int sampleRate, out VAStream stream);

    /// <summary>Creates a reverb bus that's silent until its first <see cref="VAReverbBus.SetParams"/></summary>
    public abstract VAReverbBus CreateReverbBus();

    /// <summary>Opens a capture device (empty name = system default). onData receives a pointer to the captured frames and the frame count. Throws if the device fails to open</summary>
    public abstract VACaptureDevice OpenCaptureDevice(string deviceName, VAPcmFormat format, int sampleRate, int bufferFrames, Action<nint, int> onData);

    // HRTF, which only OpenAL Soft supports. Backends without it keep these defaults
    public virtual List<string> GetAvailableHrtfs() => [];
    public virtual bool SetHrtf(string name) => false;
    public virtual string GetHrtf() => "";
    public virtual string GetActiveHrtf() => "";
    public virtual bool SetHrtfEnabled(bool value) => false;
    public virtual bool GetHrtfEnabled() => false;
}
