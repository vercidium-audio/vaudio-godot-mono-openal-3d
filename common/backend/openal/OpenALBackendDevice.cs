// Human-created

namespace vaudio_godot_mono_openal;

public sealed partial class OpenALBackend
{
    const string SYSTEM_DEFAULT = "";
    const string SETTINGS_PREFIX = "audio/vaudio/";

    string _outputDeviceName = SYSTEM_DEFAULT;

    int maximumAuxiliarySends;
    int sampleRate;
    bool hrtfEnabled;
    int maximumMonoSources;
    int maximumStereoSources;

    bool _settingsRead;

    // Settings helpers
    Variant GetSetting(string name, Variant fallback) => ProjectSettings.GetSetting($"{SETTINGS_PREFIX}{name}", fallback);

    int GetIntSetting(string name, int fallback) => GetSetting(name, fallback).AsInt32();
    bool GetBoolSetting(string name, bool fallback) => GetSetting(name, fallback).AsBool();
    string GetStringSetting(string name, string fallback) => GetSetting(name, fallback).AsString();

    void EnsureSettingsRead()
    {
        // Only run once, so settings aren't lost when recreating a device
        if (_settingsRead)
            return;

        _settingsRead = true;

        var deviceNameSetting = GetStringSetting("output_device", SYSTEM_DEFAULT);
        _outputDeviceName = deviceNameSetting == "System Default" ? SYSTEM_DEFAULT : deviceNameSetting;

        maximumAuxiliarySends = Math.Max(1, GetIntSetting("openal_max_reverb_sends", 1));
        sampleRate = GetIntSetting("sample_rate", 0);
        hrtfEnabled = GetBoolSetting("openal_hrtf_enabled", true);
        maximumMonoSources = GetIntSetting("openal_max_mono_sources", 16);
        maximumStereoSources = GetIntSetting("openal_max_stereo_sources", 240);

        var hrtfSetting = GetStringSetting("openal_hrtf", DefaultHrtfLabel);
        _hrtfName = hrtfSetting == DefaultHrtfLabel ? SYSTEM_DEFAULT : hrtfSetting;
    }

    void CreateDeviceAndContext()
    {
        Debug.Assert(!Engine.IsEditorHint());

        Debug.Assert(ALContext == null);
        Debug.Assert(ALDevice == null);

        EnsureSettingsRead();
        PrepareHrtfFolder();

        ALDevice = new(string.IsNullOrEmpty(_outputDeviceName) ? null : _outputDeviceName);

        // Create an OpenAL context
        var settings = new ALContextSettings()
        {
            HRTFEnabled = hrtfEnabled,
            HRTFID = FindHrtfIndex(),
            SampleRate = sampleRate,
            MaximumAuxiliarySends = maximumAuxiliarySends,
            MaximumMonoSources = maximumMonoSources,
            MaximumStereoSources = maximumStereoSources,
            LogWarning = LogWarning,
        };

        ALContext = new(ALDevice, settings);

        // Disable HRTF
        if (!hrtfEnabled && ALDevice.HasExtension(HrtfExtension))
            AL.ResetDeviceSOFT(ALDevice.handle, BuildAttribs());

        ApplyAllSettings();
    }

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

    // Returns false if the device was lost and couldn't be recreated
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

        // Try a quick device reopen first
        if (ALDevice.Reopen(string.IsNullOrEmpty(_outputDeviceName) ? null : _outputDeviceName, BuildAttribs()))
            return;

        RecreateDeviceFully();
    }

    void RecreateDeviceFully()
    {
        DestroyDevice();
        CreateDeviceAndContext();

        // Re-play sounds that were playing before the device changed/disconnected
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
