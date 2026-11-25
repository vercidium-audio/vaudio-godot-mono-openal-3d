// AI-created

namespace vaudio_godot_mono_openal;

public sealed partial class OpenALBackend
{
    // Empty means the system default
    string _outputDeviceName = "";

    int _maximumAuxiliarySends;
    int _sampleRate;
    bool _hrtfEnabled;
    int _maximumMonoSources;
    int _maximumStereoSources;

    bool _settingsRead;

    // Read once, so runtime changes (SetOutputDevice, SetHrtf etc.) aren't overwritten when the device is recreated
    void EnsureSettingsRead()
    {
        if (_settingsRead)
            return;

        _settingsRead = true;

        // plugin_main.gd only registers these settings in the editor, and project.godot doesn't store settings left at their default, so a running game must supply the same defaults itself
        var deviceNameSetting = ProjectSettings.GetSetting("audio/vaudio/output_device", "").AsString();
        _outputDeviceName = deviceNameSetting == "System Default" ? "" : deviceNameSetting;

        _maximumAuxiliarySends = Math.Max(1, ProjectSettings.GetSetting("audio/vaudio/openal_max_reverb_sends", 1).AsInt32());
        _sampleRate = ProjectSettings.GetSetting("audio/vaudio/sample_rate", 0).AsInt32();
        _hrtfEnabled = ProjectSettings.GetSetting("audio/vaudio/openal_hrtf_enabled", true).AsBool();
        _maximumMonoSources = ProjectSettings.GetSetting("audio/vaudio/openal_max_mono_sources", 16).AsInt32();
        _maximumStereoSources = ProjectSettings.GetSetting("audio/vaudio/openal_max_stereo_sources", 240).AsInt32();

        var hrtfSetting = ProjectSettings.GetSetting("audio/vaudio/openal_hrtf", DefaultHrtfLabel).AsString();
        _hrtfName = hrtfSetting == DefaultHrtfLabel ? "" : hrtfSetting;
    }

    void CreateDeviceAndContext()
    {
        // Shouldn't be initialising in the editor
        Debug.Assert(!Engine.IsEditorHint());

        Debug.Assert(ALContext == null);
        Debug.Assert(ALDevice == null);

        EnsureSettingsRead();
        PrepareHrtfFolder();

        ALDevice = new(string.IsNullOrEmpty(_outputDeviceName) ? null : _outputDeviceName);

        // Create an OpenAL context
        var settings = new ALContextSettings()
        {
            HRTFEnabled = _hrtfEnabled,
            HRTFID = FindHrtfIndex(),
            SampleRate = _sampleRate,
            MaximumAuxiliarySends = _maximumAuxiliarySends,
            MaximumMonoSources = _maximumMonoSources,
            MaximumStereoSources = _maximumStereoSources,
            LogWarning = LogWarning,
        };

        ALContext = new(ALDevice, settings);

        // ALContextSettings leaves HRTF up to OpenAL Soft's headphone detection when it's disabled, so explicitly turn it off
        if (!_hrtfEnabled && ALDevice.HasExtension(HrtfExtension))
            AL.ResetDeviceSOFT(ALDevice.handle, BuildAttribs());

        ApplyAllSettings();
    }

    // Releases everything on the device (via DeviceDestroying), then the context and device
    void DestroyDevice()
    {
        RaiseDeviceDestroying();

        AL.MakeContextCurrent(IntPtr.Zero);
        ALContext.Destroy();
        ALContext = null;

        ALDevice.Close();
        ALDevice = null;
    }

    const string DisconnectExtension = "ALC_EXT_disconnect";
    const ulong ConnectionCheckIntervalMs = 1000;
    ulong _nextConnectionCheckMs;

    // Once the output device is lost (e.g. headphones unplugged), OpenAL Soft stops every source and new ones stop as soon as they play. Recreates the device so looping sources replay. Returns false if the device was lost and couldn't be recreated
    bool CheckDeviceConnected()
    {
        var now = Time.GetTicksMsec();
        if (now < _nextConnectionCheckMs)
            return true;

        _nextConnectionCheckMs = now + ConnectionCheckIntervalMs;

        if (!ALDevice.HasExtension(DisconnectExtension) || ALDevice.GetIntegerALC(AL.ALC_CONNECTED) != 0)
            return true;

        LogWarning("The audio output device was disconnected, recreating it");

        try
        {
            RecreateDeviceFully();
            return true;
        }
        catch (Exception e)
        {
            LogError($"Failed to recreate the audio output device after it was disconnected: {e.Message}");
            return false;
        }
    }

    void RecreateDevice()
    {
        // Don't create OpenAL devices when changing properties in the editor
        if (!IsInitialised)
            return;

        // Try a reopen first. If it fails, destroy and recreate the device
        if (ALDevice.Reopen(string.IsNullOrEmpty(_outputDeviceName) ? null : _outputDeviceName, BuildAttribs()))
            return;

        RecreateDeviceFully();
    }

    void RecreateDeviceFully()
    {
        DestroyDevice();
        CreateDeviceAndContext();
        RaiseDeviceRecreated();
    }

    public override void SetOutputDevice(string name)
    {
        EnsureSettingsRead();
        _outputDeviceName = name;
        RecreateDevice();
    }

    public override string[] GetOutputDevices() => [.. AL.GetStringList(IntPtr.Zero, AL.ALC_ALL_DEVICES_SPECIFIER)];

    public override void RefreshSettingHints() => RefreshHrtfList();
}
