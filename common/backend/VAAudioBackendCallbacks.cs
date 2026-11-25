namespace vaudio_godot_mono_openal;

public static partial class VAAudioBackend
{
    // Callbacks for when the device is destroyed (e.g. when switching audio devices)
    static HashSet<Action> OnDeviceDestroyedCallbacks = [];

    // Callbacks for when the device is recreated (e.g. after switching audio devices)
    static HashSet<Action> OnDeviceRecreatedCallbacks = [];

    /// <summary>
    /// Register a callback to be invoked when the audio device is destroyed.
    /// Use this to clean up any backend resources (reverb buses, etc.)
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
    /// Register a callback to be invoked when the audio device is recreated.
    /// Use this to recreate any backend resources (reverb buses, etc.) after the device is ready.
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

    static void DestroyAllAudioSources(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is VAPlayer source)
                source.OnDeviceDestroyed();

            DestroyAllAudioSources(child);
        }
    }

    // Releases everything that lives on the device, before the backend destroys it
    static void OnDeviceDestroying()
    {
        CancelLoadingSounds();

        DestroyAllAudioSources(((SceneTree)Engine.GetMainLoop()).Root);

        // Invoke device destroyed callbacks (e.g. for cleaning up reverb effects)
        foreach (var callback in OnDeviceDestroyedCallbacks)
            callback.Invoke();
    }

    static void OnDeviceRecreated()
    {
        // Invoke device recreated callbacks (e.g. for recreating reverb effects)
        foreach (var callback in OnDeviceRecreatedCallbacks)
            callback.Invoke();
    }
}
