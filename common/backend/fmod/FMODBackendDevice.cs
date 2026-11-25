// AI-created

namespace vaudio_godot_mono_openal;

public sealed partial class FMODBackend
{
    // Empty means the system default
    string _outputDeviceName = "";

    string _libraryPath;
    int _sampleRate;
    int _maxChannels;
    int _dspBufferSize;

    bool _settingsRead;

    // Read once, so a runtime SetOutputDevice isn't overwritten
    void EnsureSettingsRead()
    {
        if (_settingsRead)
            return;

        _settingsRead = true;

        // plugin_main.gd only registers these settings in the editor, and project.godot doesn't store settings left at their default, so a running game must supply the same defaults itself
        var deviceNameSetting = ProjectSettings.GetSetting("audio/vaudio/output_device", "").AsString();
        _outputDeviceName = deviceNameSetting == "System Default" ? "" : deviceNameSetting;

        _sampleRate = ProjectSettings.GetSetting("audio/vaudio/sample_rate", 0).AsInt32();
        _libraryPath = ProjectSettings.GetSetting("audio/vaudio/fmod_library_path", "").AsString();
        _maxChannels = Math.Clamp(ProjectSettings.GetSetting("audio/vaudio/fmod_max_channels", 512).AsInt32(), 1, 4095);
        _dspBufferSize = ProjectSettings.GetSetting("audio/vaudio/fmod_dsp_buffer_size", 0).AsInt32();
    }

    public override void SetOutputDevice(string name)
    {
        EnsureSettingsRead();
        _outputDeviceName = name;

        // FMOD moves every playing sound over to the new driver itself
        if (IsInitialised)
            SelectDriver(_system);
    }

    void SelectDriver(FMOD.System system)
    {
        if (string.IsNullOrEmpty(_outputDeviceName))
        {
            Check(system.setDriver(0), "System::setDriver");
            return;
        }

        int index = Array.IndexOf(GetDriverNames(system), _outputDeviceName);

        if (index < 0)
        {
            LogWarning($"The audio output device '{_outputDeviceName}' wasn't found, so the system default is used");
            index = 0;
        }

        Check(system.setDriver(index), "System::setDriver");
    }

    string[] GetDriverNames(FMOD.System system)
    {
        if (!Check(system.getNumDrivers(out int count), "System::getNumDrivers"))
            return [];

        var names = new string[count];

        for (int i = 0; i < count; i++)
        {
            Check(system.getDriverInfo(i, out string name, 256, out _, out _, out _, out _), "System::getDriverInfo");
            names[i] = name ?? "";
        }

        return names;
    }

    public override string[] GetOutputDevices() => WithSystem(GetDriverNames, false);

    // VAInputStreamSource's device_name dropdown. Unplugged drivers are left out
    public override string[] GetCaptureDevices() => WithSystem(system => [.. GetRecordDrivers(system).FindAll(d => d.Connected).ConvertAll(d => d.Name)], true);

    // The record driver to capture from (empty name = the system default), or -1 if there's no such connected driver
    internal int FindRecordDriver(string name)
    {
        foreach (var driver in GetRecordDrivers(_system))
        {
            if (driver.Connected && (string.IsNullOrEmpty(name) ? driver.IsDefault : driver.Name == name))
                return driver.Index;
        }

        return -1;
    }

    record struct RecordDriver(int Index, string Name, bool Connected, bool IsDefault);

    List<RecordDriver> GetRecordDrivers(FMOD.System system)
    {
        var drivers = new List<RecordDriver>();

        if (!Check(system.getRecordNumDrivers(out int count, out _), "System::getRecordNumDrivers"))
            return drivers;

        for (int i = 0; i < count; i++)
        {
            if (Check(system.getRecordDriverInfo(i, out string name, 256, out _, out _, out _, out _, out var state), "System::getRecordDriverInfo"))
                drivers.Add(new(i, name ?? "", state.HasFlag(FMOD.DRIVER_STATE.CONNECTED), state.HasFlag(FMOD.DRIVER_STATE.DEFAULT)));
        }

        return drivers;
    }

    // Also called in the editor (for the device dropdowns), where the system isn't initialised, so a temporary one lists the drivers. Record drivers are only enumerated once it's initialised (FMOD returns ERR_UNINITIALIZED before), which opens the output device briefly
    string[] WithSystem(Func<FMOD.System, string[]> list, bool initialise)
    {
        if (IsInitialised)
            return list(_system);

        EnsureSettingsRead();

        if (!LoadLibrary() || !Check(FMOD.Factory.System_Create(out var system), "System_Create"))
            return [];

        string[] names = [];

        if (!initialise || Check(system.init(1, FMOD.INITFLAGS.NORMAL, IntPtr.Zero), "System::init"))
            names = list(system);

        system.release();
        return names;
    }

    // FMOD has no backend-specific dropdowns
    public override void RefreshSettingHints() { }
}
