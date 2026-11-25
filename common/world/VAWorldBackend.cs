// AI-created

namespace vaudio_godot_mono_openal;

// The audio backend's global settings, plus static passthroughs to it for GDScript, since VAAudioBackend is a plain C# class
public partial class VAWorld
{
    public static void RefreshOutputDeviceList() => VAAudioBackend.RefreshDeviceLists();

    public static string[] GetAvailableHrtfs() => VAAudioBackend.GetAvailableHrtfs().ToArray();
    public static bool SetHrtf(string name) => VAAudioBackend.SetHrtf(name);
    public static string GetHrtf() => VAAudioBackend.GetHrtf();
    public static string GetActiveHrtf() => VAAudioBackend.GetActiveHrtf();
    public static bool SetHrtfEnabled(bool value) => VAAudioBackend.SetHrtfEnabled(value);
    public static bool GetHrtfEnabled() => VAAudioBackend.GetHrtfEnabled();

    // How many audio backend errors have been logged since the game started
    public static int GetBackendErrorCount() => VAAudioBackend.ErrorCount;

    // The audio backend in use (e.g. "OpenAL", "FMOD"), or empty if none is compiled in
    public static string GetBackendName() => VAAudioBackend.Backend?.Name ?? "";

    public static bool IsBackendInitialised() => VAAudioBackend.Initialised;

    // The global mix settings the audio backend is using, keyed by their VAWorld property names, so tests can check a VAWorld's values reached it
    public static Godot.Collections.Dictionary GetBackendSettings() => new()
    {
        { nameof(MasterVolume), VAAudioBackend.MasterVolume },
        { nameof(DistanceModel), (int)VAAudioBackend.DistanceModel },
        { nameof(DopplerScale), VAAudioBackend.DopplerScale },
        { nameof(MetersPerUnit), VAAudioBackend.MetersPerUnit },
        { nameof(SpeedOfSound), VAAudioBackend.SpeedOfSound },
        { nameof(ReverbOnly), VAAudioBackend.ReverbOnly },
    };

    // The backend's settings are global, so the last VAWorld to enter the tree pushes all of its own
    void ApplyBackendSettings()
    {
        VAAudioBackend.MasterVolume = MasterVolume;
        VAAudioBackend.DistanceModel = DistanceModel;
        VAAudioBackend.DopplerScale = DopplerScale;
        VAAudioBackend.MetersPerUnit = MetersPerUnit;
        VAAudioBackend.SpeedOfSound = SpeedOfSound;
        VAAudioBackend.ReverbOnly = ReverbOnly;
    }
}
