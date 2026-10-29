namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    const string DefaultDeviceLabel = "System Default";

    static int _maximumAuxiliarySends;
    static int _sampleRate;
    static bool _hrtfEnabled;
    static int _maximumMonoSources;
    static int _maximumStereoSources;

    static bool _settingsRead;

    // Read once, so runtime changes (SetOutputDevice, SetHrtf etc.) aren't overwritten when the device is recreated
    static void EnsureSettingsRead()
    {
        if (_settingsRead)
            return;

        _settingsRead = true;

        // plugin_main.gd only registers these settings in the editor, and project.godot doesn't store settings left at their default, so a running game must supply the same defaults itself
        var deviceNameSetting = ProjectSettings.GetSetting("audio/vaudio/output_device", DefaultDeviceLabel).AsString();
        _outputDeviceName = deviceNameSetting == DefaultDeviceLabel ? "" : deviceNameSetting;

        _maximumAuxiliarySends = Math.Max(1, ProjectSettings.GetSetting("audio/vaudio/max_reverb_sends", 1).AsInt32());
        _sampleRate = ProjectSettings.GetSetting("audio/vaudio/sample_rate", 0).AsInt32();
        _hrtfEnabled = ProjectSettings.GetSetting("audio/vaudio/hrtf_enabled", true).AsBool();
        _maximumMonoSources = ProjectSettings.GetSetting("audio/vaudio/max_mono_sources", 16).AsInt32();
        _maximumStereoSources = ProjectSettings.GetSetting("audio/vaudio/max_stereo_sources", 240).AsInt32();

        var hrtfSetting = ProjectSettings.GetSetting("audio/vaudio/hrtf", DefaultHrtfLabel).AsString();
        _hrtfName = hrtfSetting == DefaultHrtfLabel ? "" : hrtfSetting;
    }

    static void CreateDeviceAndContext()
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

        // Set initial properties
        SetMetersPerUnit(MetersPerUnit);
        SetSpeedOfSound(SpeedOfSound);
        SetListenerGain(MasterVolume);
        SetDistanceModel(DistanceModel);
    }

    static void RecreateDevice()
    {
        // Don't create OpenAL devices when changing properties in the editor
        if (!Initialised)
            return;

        // Try a reopen first. If it fails, destroy and recreate the device
        if (ALDevice.Reopen(string.IsNullOrEmpty(_outputDeviceName) ? null : _outputDeviceName, BuildAttribs()))
            return;

        RecreateDeviceFully();
    }

    static void RecreateDeviceFully()
    {
        CancelLoadingAndDestroy();
        CreateDeviceAndContext();

        // Invoke device recreated callbacks (e.g. for recreating reverb effects)
        foreach (var callback in OnDeviceRecreatedCallbacks)
            callback.Invoke();
    }

    public static void RefreshDeviceLists()
    {
        OutputDeviceList = AL.GetStringList(IntPtr.Zero, AL.ALC_ALL_DEVICES_SPECIFIER);

        var devices = new List<string> { DefaultDeviceLabel };
        devices.AddRange(OutputDeviceList);

        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", "audio/vaudio/output_device" },
            { "type", (int)Variant.Type.String },
            { "hint", (int)PropertyHint.Enum },
            { "hint_string", string.Join(",", devices) }
        });

        RefreshHrtfList();
    }
}
