namespace vaudio_godot_mono_openal;

/// <summary>
/// Abstraction over the audio playback engine (OpenAL Soft or FMOD). All shared vaudio code talks to the backend through this interface via AudioManager.Backend instead of calling ALManager / AL.* directly.
///
/// The listener transform is passed as raw vectors, not pitch/yaw - each dimension's VAWorldGodot already owns the pitch/yaw -> forward/up conversion (3D) or the 2D rotation, so the interface stays dimension-agnostic.
/// </summary>
public interface IAudioBackend
{
    bool Initialised { get; }

    /// <summary>Create the device/context if not already up. No-op in the editor or if already initialised.</summary>
    void Ensure();

    /// <summary>Pump the backend once per frame (process context, dispose finished voices).</summary>
    void Update();

    void SetListenerPosition(Vector3 position);
    void SetListenerVelocity(Vector3 velocity);

    /// <summary>Orientation as forward + up unit vectors (the caller converts from pitch/yaw or 2D rotation).</summary>
    void SetListenerOrientation(Vector3 forward, Vector3 up);

    void SetMasterVolume(float volume);
    void SetDistanceModel(AudioDistanceModel model);
    void SetMetersPerUnit(float metersPerUnit);
    void SetSpeedOfSound(float speedOfSound);

    /// <summary>When true, a voice's direct (dry) path is silenced and only its reverb send is audible.</summary>
    void SetReverbOnly(bool value);

    bool ReverbOnly { get; }

    /// <summary>Wrap already-decoded PCM (from the shared AudioBuffer path) in a backend sound. Decode itself is backend-agnostic and stays in common.</summary>
    IAudioBuffer GetOrCreateBuffer(AudioStream stream);

    IAudioReverbSlot CreateReverbSlot();
    IAudioFilterHandle CreateFilter(float gain, float gainHF);

    void RegisterDeviceDestroyedCallback(Action callback);
    void UnregisterDeviceDestroyedCallback(Action callback);
    void RegisterDeviceRecreatedCallback(Action callback);
    void UnregisterDeviceRecreatedCallback(Action callback);

    string[] GetOutputDevices();
    void SetOutputDevice(string deviceName);
}
