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

    // Also called in the editor (for the output_device dropdown), where the system isn't initialised, so a temporary uninitialised one lists the drivers
    public override string[] GetOutputDevices()
    {
        if (IsInitialised)
            return GetDriverNames(_system);

        EnsureSettingsRead();

        if (!LoadLibrary() || !Check(FMOD.Factory.System_Create(out var system), "System_Create"))
            return [];

        var names = GetDriverNames(system);
        system.release();
        return names;
    }

    // FMOD has no backend-specific dropdowns
    public override void RefreshSettingHints() { }
}
