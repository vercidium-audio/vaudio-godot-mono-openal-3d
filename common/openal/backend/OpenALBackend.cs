using OpenALManagedSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

/// <summary>
/// OpenAL Soft implementation of <see cref="IAudioBackend"/>.
///
/// For now this mostly delegates to the existing <see cref="ALManager"/> static and the raw <c>AL.*</c> API - code is not moved out of ALManager yet (that happens in later plan steps). There is only ever one OpenAL device/context and one listener, so the listener and global-property setters talk to <c>AL.*</c> directly rather than through ALManager's per-dimension listener partial.
/// </summary>
public class OpenALBackend : IAudioBackend
{
    public bool Initialised => ALManager.Initialised;

    public void Ensure() => ALManager.Ensure();
    public void Update() => ALManager.Update();

    // --- Listener (single listener, single context - straight to AL) ---

    public void SetListenerPosition(Vector3 p) => AL.Listenerfv(AL.AL_POSITION, [p.X, p.Y, p.Z]);

    public void SetListenerVelocity(Vector3 v) => AL.Listenerfv(AL.AL_VELOCITY, [v.X, v.Y, v.Z]);

    public void SetListenerOrientation(Vector3 forward, Vector3 up) =>
        AL.Listenerfv(AL.AL_ORIENTATION, [forward.X, forward.Y, forward.Z, up.X, up.Y, up.Z]);

    // --- Global properties ---

    public void SetMasterVolume(float volume) => AL.Listenerf(AL.AL_GAIN, MathF.Max(0, volume));

    public void SetMetersPerUnit(float metersPerUnit) => AL.Listenerf(AL.AL_METERS_PER_UNIT, MathF.Max(0, metersPerUnit));

    public void SetSpeedOfSound(float speedOfSound) => AL.SpeedOfSound(MathF.Max(0, speedOfSound));

    public void SetDistanceModel(AudioDistanceModel model) => AL.DistanceModel((int)ToALDistanceModel(model));

    bool _reverbOnly;
    public bool ReverbOnly => _reverbOnly;
    public void SetReverbOnly(bool value) => _reverbOnly = value;

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

    public IAudioBuffer GetOrCreateBuffer(AudioStream stream) => ALManager.GetOrCreateBuffer(stream);

    public IAudioReverbSlot CreateReverbSlot() => new OpenALReverbSlot();

    public IAudioFilterHandle CreateFilter(float gain, float gainHF) => new OpenALFilterHandle(gain, gainHF);

    // --- Device callbacks (ALManager owns the lists - dimension-agnostic) ---

    public void RegisterDeviceDestroyedCallback(Action callback) => ALManager.RegisterDeviceDestroyedCallback(callback);
    public void UnregisterDeviceDestroyedCallback(Action callback) => ALManager.UnregisterDeviceDestroyedCallback(callback);
    public void RegisterDeviceRecreatedCallback(Action callback) => ALManager.RegisterDeviceRecreatedCallback(callback);
    public void UnregisterDeviceRecreatedCallback(Action callback) => ALManager.UnregisterDeviceRecreatedCallback(callback);

    // --- Output devices ---

    public string[] GetOutputDevices() => [.. AL.GetStringList(IntPtr.Zero, AL.ALC_ALL_DEVICES_SPECIFIER)];

    public void SetOutputDevice(string deviceName) => ALManager.SetOutputDevice(deviceName);
}
