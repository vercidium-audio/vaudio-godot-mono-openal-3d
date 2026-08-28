using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    // AL resources
    static ALDevice ALDevice;
    static ALContext ALContext;

    // Device cache
    static List<string> OutputDeviceList = [];

    // Keep track of all currently playing sources, so we can destroy them when they've finished playing
    static List<OpenALSource> ActiveSources = [];

    // Callbacks for when the device is destroyed (e.g. when switching audio devices)
    static HashSet<Action> OnDeviceDestroyedCallbacks = [];

    // Callbacks for when the device is recreated (e.g. after switching audio devices)
    static HashSet<Action> OnDeviceRecreatedCallbacks = [];

    /// <summary>
    /// Register a callback to be invoked when the OpenAL device is destroyed.
    /// Use this to clean up any OpenAL resources (filters, effects, etc.)
    /// </summary>
    public static void RegisterDeviceDestroyedCallback(Action callback)
    {
        if (callback == null)
            throw new ArgumentException("callback cannot be null");

        OnDeviceDestroyedCallbacks.Add(callback);
    }

    /// <summary>
    /// Unregister a previously registered device destroyed callback.
    /// </summary>
    public static void UnregisterDeviceDestroyedCallback(Action callback)
    {
        if (callback == null)
            throw new ArgumentException("callback cannot be null");

        OnDeviceDestroyedCallbacks.Remove(callback);
    }

    /// <summary>
    /// Register a callback to be invoked when the OpenAL device is recreated.
    /// Use this to recreate any OpenAL resources (filters, effects, etc.) after the device is ready.
    /// </summary>
    public static void RegisterDeviceRecreatedCallback(Action callback)
    {
        if (callback == null)
            throw new ArgumentException("callback cannot be null");

        OnDeviceRecreatedCallbacks.Add(callback);
    }

    /// <summary>
    /// Unregister a previously registered device recreated callback.
    /// </summary>
    public static void UnregisterDeviceRecreatedCallback(Action callback)
    {
        if (callback == null)
            throw new ArgumentException("callback cannot be null");

        OnDeviceRecreatedCallbacks.Remove(callback);
    }
}
