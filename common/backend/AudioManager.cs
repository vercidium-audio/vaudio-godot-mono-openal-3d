namespace vaudio_godot_mono_openal;

/// <summary>
/// Static entry point to the active audio backend. All shared vaudio code goes through <see cref="Backend"/> (or the convenience passthroughs below) instead of touching <c>ALManager</c> / <c>AL.*</c> directly.
///
/// <see cref="Backend"/> is set by each plugin's bootstrap. Until a plugin sets it explicitly it is constructed lazily on first access - the OpenAL common repo defaults it to a new <see cref="OpenALBackend"/>. The FMOD plugins set <c>Backend = new FMODBackend()</c> in their <c>plugin_main.gd</c> before this lazy path can run.
/// </summary>
public static class AudioManager
{
    static IAudioBackend _backend;

    public static IAudioBackend Backend
    {
        get => _backend ??= new OpenALBackend();
        set => _backend = value;
    }

    // --- Convenience passthroughs used widely across the shared code ---

    public static bool Initialised => Backend.Initialised;

    public static void Ensure() => Backend.Ensure();

    public static void Update() => Backend.Update();

    public static bool ReverbOnly => Backend.ReverbOnly;

    public static IAudioBuffer GetOrCreateBuffer(AudioStream stream) => Backend.GetOrCreateBuffer(stream);

    public static IAudioReverbSlot CreateReverbSlot() => Backend.CreateReverbSlot();

    public static IAudioFilterHandle CreateFilter(float gain, float gainHF) => Backend.CreateFilter(gain, gainHF);

    public static void RegisterDeviceDestroyedCallback(Action callback) => Backend.RegisterDeviceDestroyedCallback(callback);
    public static void UnregisterDeviceDestroyedCallback(Action callback) => Backend.UnregisterDeviceDestroyedCallback(callback);
    public static void RegisterDeviceRecreatedCallback(Action callback) => Backend.RegisterDeviceRecreatedCallback(callback);
    public static void UnregisterDeviceRecreatedCallback(Action callback) => Backend.UnregisterDeviceRecreatedCallback(callback);
}
