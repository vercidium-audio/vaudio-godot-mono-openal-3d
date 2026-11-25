// AI-created

namespace vaudio_godot_mono_openal;

// Static facade over the audio backend selected by audio/vaudio/backend. Nodes go through this rather than a backend type
public static partial class VAAudioBackend
{
    static VAAudioBackendBase _backend;
    static bool _backendCreated;

    /// <summary>
    /// The audio backend selected by the audio/vaudio/backend project setting. Created on first use, but only opens its device in <see cref="Ensure"/>. Null if no backend is compiled in
    /// </summary>
    public static VAAudioBackendBase Backend
    {
        get
        {
            if (!_backendCreated)
            {
                _backendCreated = true;
                _backend = VAAudioBackendRegistry.CreateSelected();

                if (_backend != null)
                {
                    _backend.DeviceDestroying += OnDeviceDestroying;
                    _backend.DeviceRecreated += OnDeviceRecreated;
                }
            }

            return _backend;
        }
    }

    public static bool Initialised => Backend?.IsInitialised == true;

    public static void Ensure()
    {
        if (Engine.IsEditorHint() || Backend == null || Backend.IsInitialised)
            return;

        Backend.Initialise();
    }

    // How many backend errors have been logged since the game started, so tests can check a run hit none
    public static int ErrorCount => Backend?.ErrorCount ?? 0;

    // Logs a failed backend call and counts it in ErrorCount
    internal static void LogBackendError(string message)
    {
        if (Backend != null)
            Backend.LogError(message);
        else
            LogError(message);
    }

    public static void Update()
    {
        if (Engine.IsEditorHint() || !Initialised)
            return;

        Backend.Update();
    }

    public static float MasterVolume
    {
        get => Backend?.MasterVolume ?? 1;
        set
        {
            if (Backend != null)
                Backend.MasterVolume = value;
        }
    }

    public static VADistanceModel DistanceModel
    {
        get => Backend?.DistanceModel ?? VADistanceModel.InverseClamped;
        set
        {
            if (Backend != null)
                Backend.DistanceModel = value;
        }
    }

    public static float MetersPerUnit
    {
        get => Backend?.MetersPerUnit ?? 1;
        set
        {
            if (Backend != null)
                Backend.MetersPerUnit = value;
        }
    }

    public static float SpeedOfSound
    {
        get => Backend?.SpeedOfSound ?? 343;
        set
        {
            if (Backend != null)
                Backend.SpeedOfSound = value;
        }
    }

    public static float DopplerScale
    {
        get => Backend?.DopplerScale ?? 1;
        set
        {
            if (Backend != null)
                Backend.DopplerScale = value;
        }
    }

    // Node logic rather than a backend setting: sources silence their own direct path
    public static bool ReverbOnly { get; set; }

    const string DefaultDeviceLabel = "System Default";

    /// <summary>
    /// Switch to the named output device, or the system default when empty
    /// </summary>
    public static void SetOutputDevice(string deviceName) => Backend?.SetOutputDevice(deviceName == DefaultDeviceLabel ? "" : deviceName ?? "");

    // Fills the audio/vaudio/output_device dropdown and the backend's own setting dropdowns. Called by the editor plugin
    public static void RefreshDeviceLists()
    {
        if (Backend == null)
            return;

        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", "audio/vaudio/output_device" },
            { "type", (int)Variant.Type.String },
            { "hint", (int)PropertyHint.Enum },
            { "hint_string", string.Join(",", [DefaultDeviceLabel, .. Backend.GetOutputDevices()]) }
        });

        Backend.RefreshSettingHints();
    }

    public static string[] GetCaptureDevices() => Backend?.GetCaptureDevices() ?? [];

    /// <summary>
    /// Opens a capture device (empty name = system default). onData receives a pointer to the captured frames and the frame count. Throws if the device fails to open
    /// </summary>
    public static VACaptureDevice OpenCaptureDevice(string deviceName, VAPcmFormat format, int sampleRate, int bufferFrames, Action<nint, int> onData)
    {
        if (Backend == null)
            throw new InvalidOperationException("no audio backend is available");

        return Backend.OpenCaptureDevice(deviceName, format, sampleRate, bufferFrames, onData);
    }

    /// <summary>
    /// Get the names of the HRTFs the backend can use. Pass one of these to SetHrtf(). Empty if the backend has no HRTF support
    /// </summary>
    public static List<string> GetAvailableHrtfs() => Backend?.GetAvailableHrtfs() ?? [];

    /// <summary>
    /// Switch to the named HRTF at runtime, without interrupting playing sounds. Pass an empty string to use the default HRTF. Returns false if the device couldn't be reset, or the backend has no HRTF support
    /// </summary>
    public static bool SetHrtf(string name) => Backend?.SetHrtf(name) ?? false;

    /// <summary>
    /// Get the HRTF name last passed to SetHrtf() or set in audio/vaudio/openal_hrtf, or an empty string for the default HRTF
    /// </summary>
    public static string GetHrtf() => Backend?.GetHrtf() ?? "";

    /// <summary>
    /// Get the name of the HRTF actually in use, or an empty string if HRTF is not active
    /// </summary>
    public static string GetActiveHrtf() => Backend?.GetActiveHrtf() ?? "";

    /// <summary>
    /// Turn HRTF on or off at runtime, without interrupting playing sounds. Returns false if the device couldn't be reset, or the backend has no HRTF support
    /// </summary>
    public static bool SetHrtfEnabled(bool value) => Backend?.SetHrtfEnabled(value) ?? false;

    /// <summary>
    /// Get whether HRTF is requested, from SetHrtfEnabled() or audio/vaudio/openal_hrtf_enabled
    /// </summary>
    public static bool GetHrtfEnabled() => Backend?.GetHrtfEnabled() ?? false;
}
