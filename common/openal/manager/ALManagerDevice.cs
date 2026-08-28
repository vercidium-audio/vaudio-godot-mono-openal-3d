namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    // Matches va_device_name.h's DEFAULT_DEVICE_LABEL in the native Godot plugin - the
    // audio/vaudio/output_device Project Setting stores this label rather than "" (a strict
    // PROPERTY_HINT_ENUM dropdown must always show the current value as one of its own entries),
    // translated back to "" ("driver default") only when read here.
    const string DefaultDeviceLabel = "System Default";

    static int _maximumAuxiliarySends;
    static int _sampleRate;
    static bool _hrtfEnabled;
    static int _maximumMonoSources;
    static int _maximumStereoSources;

    // Reads audio/vaudio/output_device, audio/vaudio/max_reverb_sends, audio/vaudio/sample_rate,
    // audio/vaudio/hrtf_enabled, audio/vaudio/max_mono_sources and audio/vaudio/max_stereo_sources
    // once - matches native's read_settings_from_project_settings() in al_manager.cpp. plugin.gd's
    // _register_project_settings() registers these (with defaults) before the singleton is
    // created, so every setting already exists here.
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

        // Create an OpenAL device - null (not "") means "use the driver default": the P/Invoke
        // marshals a C# null to a native NULL, which alcOpenDevice requires for its own "driver
        // default" behaviour, whereas "" marshals to a valid pointer to an empty C string and
        // fails - matches native's `device_name.empty() ? nullptr : device_name.c_str()`.
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

        // Diagnostic: how many auxiliary sends did the driver actually grant vs. what we asked
        // for (_maximumAuxiliarySends)? ALC_MAX_AUXILIARY_SENDS is a request, not a guarantee -
        // OpenAL Soft can silently grant fewer (or 0) if EFX isn't available on this device.
        var grantedAuxSends = ALDevice.GetIntegerALC(AL.ALC_MAX_AUXILIARY_SENDS);
        Log($"Requested {_maximumAuxiliarySends} auxiliary sends, driver granted {grantedAuxSends}");


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

        // Prefer ALDevice.Reopen (ALC_SOFT_reopen_device) - it redirects the existing ALC
        // device/context to the new output device in place, so every existing AL object (sources,
        // buffers, filters, effects) stays valid and DecodedStreams doesn't need re-decoding.
        // Reopen itself returns false (no exception) if the extension isn't present on this
        // device, in which case fall back to the old CancelLoadingAndDestroy()+
        // CreateDeviceAndContext() teardown/recreate below, which invalidates all of those and
        // fires the device destroyed/recreated callbacks. Matches native's ALManager::reinitialize
        // (al_manager.cpp, vaudio-godot-native-openal-3d-source).
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

        if (ALDevice.Reopen(string.IsNullOrEmpty(_outputDeviceName) ? null : _outputDeviceName, attribs))
            return;

        CancelLoadingAndDestroy();
        CreateDeviceAndContext();

        // Invoke device recreated callbacks (e.g. for recreating reverb effects)
        foreach (var callback in OnDeviceRecreatedCallbacks)
            callback.Invoke();
    }

    // Refreshes OutputDeviceList and the audio/vaudio/output_device Project Setting's
    // PROPERTY_HINT_ENUM hint_string from the real OpenAL device list - matches native's
    // refresh_output_device_hint(). plugin.gd's _enter_tree() calls this directly (both in-editor
    // and at game runtime) after _register_project_settings(), since ALManager is a static class
    // with no Node lifecycle of its own to hook this from.
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
