// Human-created

namespace vaudio_godot_mono_openal;

/// <summary>Abstract base for any audio backend (OpenAL, FMOD, etc)</summary>
public abstract class VAAudioBackendBase
{
    /// <summary>The name of this backend in the "audio/vaudio/backend" project setting dropdown.</summary>
    public abstract string Name { get; }

    public abstract bool IsInitialised { get; }

    /// <summary>Initialise the backend and output device</summary>
    public abstract void Initialise();

    /// <summary>Call this every frame</summary>
    public abstract void Update();

    /// <summary>Invokes <see cref="DeviceDestroying"/> then closes the output device and backend</summary>
    public abstract void Shutdown();

    int _errorCount;

    /// <summary>How many backend errors have been logged since the game started</summary>
    public int ErrorCount => System.Threading.Volatile.Read(ref _errorCount);

    /// <summary>Log a failed backend call and increment <see cref="ErrorCount"/></summary>
    public void LogError(string message)
    {
        System.Threading.Interlocked.Increment(ref _errorCount);
        Logger.LogError(message);
    }

    /// <summary>Raised before the device is destroyed (e.g. when switching output devices)</summary>
    public event Action DeviceDestroying;

    /// <summary>Raised once the device has been recreated, so sounds can resume playing</summary>
    public event Action DeviceRecreated;

    protected void RaiseDeviceDestroying() => DeviceDestroying?.Invoke();
    protected void RaiseDeviceRecreated() => DeviceRecreated?.Invoke();

    float masterVolume = 1;
    VADistanceModel distanceModel = VADistanceModel.InverseClamped;
    float metersPerUnit = 1;
    float speedOfSound = 343;
    float dopplerScale = 1;

    public float MasterVolume
    {
        get => masterVolume;
        set => UpdateProperty(ref masterVolume, MathF.Max(0, value), ApplyMasterVolume);
    }

    public VADistanceModel DistanceModel
    {
        get => distanceModel;
        set => UpdateProperty(ref distanceModel, value, ApplyDistanceModel);
    }

    public float MetersPerUnit
    {
        get => metersPerUnit;
        set => UpdateProperty(ref metersPerUnit, MathF.Max(0, value), ApplyMetersPerUnit);
    }

    public float SpeedOfSound
    {
        get => speedOfSound;
        set => UpdateProperty(ref speedOfSound, MathF.Max(0, value), ApplySpeedOfSound);
    }

    public float DopplerScale
    {
        get => dopplerScale;
        set => UpdateProperty(ref dopplerScale, MathF.Max(0, value), ApplyDopplerScale);
    }

    Vector3 listenerPosition;
    Vector3 listenerVelocity;
    Vector3 listenerForward = new(0, 0, -1);
    Vector3 listenerUp = Vector3.Up;

    public Vector3 ListenerPosition => listenerPosition;

    public void SetListenerPosition(Vector3 position) => UpdateProperty(ref listenerPosition, position, ApplyListenerPosition);
    public void SetListenerVelocity(Vector3 velocity) => UpdateProperty(ref listenerVelocity, velocity, ApplyListenerVelocity);

    public void SetListenerOrientation(Vector3 forward, Vector3 up)
    {
        if (forward == listenerForward && up == listenerUp)
            return;

        listenerForward = forward;
        listenerUp = up;

        if (IsInitialised)
            ApplyListenerOrientation(forward, up);
    }

    protected void ApplyAllSettings()
    {
        ApplyMasterVolume(masterVolume);
        ApplyDistanceModel(distanceModel);
        ApplyMetersPerUnit(metersPerUnit);
        ApplySpeedOfSound(speedOfSound);
        ApplyDopplerScale(dopplerScale);
        ApplyListenerPosition(listenerPosition);
        ApplyListenerVelocity(listenerVelocity);
        ApplyListenerOrientation(listenerForward, listenerUp);
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

    /// <summary>Switch to the provided output device, or system default if empty</summary>
    public abstract void SetOutputDevice(string name);

    public abstract string[] GetCaptureDevices();

    // TODO - can we make this 'editor-only', (wrap it in #if EDITOR?) so it's excluded from published games?
    /// <summary>Refresh the dropdowns in project settings</summary>
    public abstract void RefreshSettingHints();

    // TODO - do we support 8-bit or ambisonics / etc? Later on?
    /// <summary>Create a sound from 16-bit PCM data. Called from a worker thread.</summary>
    /// <returns>Null if the device isn't available.</returns>
    public abstract VASound CreateSound(short[] pcm16, int channels, int sampleRate);

    /// <summary>Creates a stream, e.g. for voice data</summary>
    /// <returns>true if a stream was created.</returns>
    public abstract bool TryCreateStream(VAPcmFormat format, int sampleRate, out VAStream stream);

    /// <summary>Create a reverb effect</summary>
    public abstract VAReverbEffect CreateReverbEffect();

    /// <summary>Open a capture device. onData receives a pointer to the captured frames and the frame count.</summary>
    public abstract VACaptureDevice OpenCaptureDevice(string deviceName, VAPcmFormat format, int sampleRate, int bufferFrames, Action<nint, int> onData);

    // HRTF settings for OpenAL Soft
    public virtual List<string> GetAvailableHrtfs() => [];
    public virtual bool SetHrtf(string name) => false;
    public virtual string GetHrtf() => "";
    public virtual string GetActiveHrtf() => "";
    public virtual bool SetHrtfEnabled(bool value) => false;
    public virtual bool GetHrtfEnabled() => false;
}
