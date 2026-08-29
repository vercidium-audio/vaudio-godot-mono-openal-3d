namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    const string DefaultDeviceLabel = "System Default";

    static int _maximumAuxiliarySends;
    static int _sampleRate;
    static bool _hrtfEnabled;
    static int _maximumMonoSources;
    static int _maximumStereoSources;

    static void ReadSettingsFromProjectSettings()
    {
        var deviceNameSetting = ProjectSettings.GetSetting("audio/vaudio/output_device").AsString();
        _outputDeviceName = deviceNameSetting == DefaultDeviceLabel ? "" : deviceNameSetting;

        _maximumAuxiliarySends = Math.Max(1, ProjectSettings.GetSetting("audio/vaudio/max_reverb_sends").AsInt32());
        _sampleRate = ProjectSettings.GetSetting("audio/vaudio/sample_rate").AsInt32();
        _hrtfEnabled = ProjectSettings.GetSetting("audio/vaudio/hrtf_enabled").AsBool();
        _maximumMonoSources = ProjectSettings.GetSetting("audio/vaudio/max_mono_sources").AsInt32();
        _maximumStereoSources = ProjectSettings.GetSetting("audio/vaudio/max_stereo_sources").AsInt32();
    }

    static void CreateDeviceAndContext()
    {
        // Shouldn't be initialising in the editor
        Debug.Assert(!Engine.IsEditorHint());

        Debug.Assert(ALContext == null);
        Debug.Assert(ALDevice == null);

        ReadSettingsFromProjectSettings();

        ALDevice = new(string.IsNullOrEmpty(_outputDeviceName) ? null : _outputDeviceName);

        // Create an OpenAL context
        var settings = new ALContextSettings()
        {
            HRTFEnabled = _hrtfEnabled,
            HRTFID = 0,
            SampleRate = _sampleRate,
            MaximumAuxiliarySends = _maximumAuxiliarySends,
            MaximumMonoSources = _maximumMonoSources,
            MaximumStereoSources = _maximumStereoSources,
            LogWarning = LogWarning,
        };

        ALContext = new(ALDevice, settings);

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
        
        ReadSettingsFromProjectSettings();

        var attribs = ALContext.GetAttribs(new()
        {
            HRTFEnabled = _hrtfEnabled,
            HRTFID = 0,
            SampleRate = _sampleRate,
            MaximumAuxiliarySends = _maximumAuxiliarySends,
            MaximumMonoSources = _maximumMonoSources,
            MaximumStereoSources = _maximumStereoSources,
        });

        // Try a reopen first. If it fails, destroy and recreate the device
        if (ALDevice.Reopen(string.IsNullOrEmpty(_outputDeviceName) ? null : _outputDeviceName, attribs))
            return;

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
    }
}
