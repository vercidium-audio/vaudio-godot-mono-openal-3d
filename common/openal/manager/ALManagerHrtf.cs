using System.Runtime.InteropServices;

namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    const string DefaultHrtfLabel = "Default";
    const string HrtfExtension = "ALC_SOFT_HRTF";
    const string LocalPathVariable = "ALSOFT_LOCAL_PATH";

    // Empty means OpenAL Soft's default HRTF
    static string _hrtfName = "";

    static bool _hrtfFolderPrepared;

    /// <summary>
    /// Get the names of the HRTFs OpenAL Soft can use, including any .mhr files in the audio/vaudio/hrtf_folder folder. Pass one of these to SetHrtf()
    /// </summary>
    public static List<string> GetAvailableHrtfs() => Initialised ? GetHrtfList(ALDevice.handle) : [];

    /// <summary>
    /// Switch to the named HRTF at runtime, without interrupting playing sounds. Pass an empty string to use OpenAL Soft's default HRTF. Returns false if the device couldn't be reset
    /// </summary>
    public static bool SetHrtf(string name)
    {
        EnsureSettingsRead();

        name = name == DefaultHrtfLabel ? "" : name ?? "";

        if (name == _hrtfName)
            return true;

        _hrtfName = name;
        return ResetDevice();
    }

    /// <summary>
    /// Get the HRTF name last passed to SetHrtf() or set in audio/vaudio/hrtf, or an empty string for the default HRTF
    /// </summary>
    public static string GetHrtf()
    {
        EnsureSettingsRead();
        return _hrtfName;
    }

    /// <summary>
    /// Get the name of the HRTF OpenAL Soft is actually using, or an empty string if HRTF is not active (e.g. it's disabled, or the output isn't stereo)
    /// </summary>
    public static string GetActiveHrtf()
    {
        if (!Initialised || !ALDevice.HasExtension(HrtfExtension))
            return "";

        var status = ALDevice.GetIntegerALC(AL.ALC_HRTF_STATUS_SOFT);

        if (status != AL.ALC_HRTF_ENABLED_SOFT && status != AL.ALC_HRTF_REQUIRED_SOFT)
            return "";

        return ALDevice.GetString(AL.ALC_HRTF_SPECIFIER_SOFT) ?? "";
    }

    /// <summary>
    /// Turn HRTF on or off at runtime, without interrupting playing sounds. HRTF is intended for headphones - players using speakers may prefer it off. Returns false if the device couldn't be reset
    /// </summary>
    public static bool SetHrtfEnabled(bool value)
    {
        EnsureSettingsRead();

        if (value == _hrtfEnabled)
            return true;

        _hrtfEnabled = value;
        return ResetDevice();
    }

    /// <summary>
    /// Get whether HRTF is requested, from SetHrtfEnabled() or audio/vaudio/hrtf_enabled
    /// </summary>
    public static bool GetHrtfEnabled()
    {
        EnsureSettingsRead();
        return _hrtfEnabled;
    }

    static List<string> GetHrtfList(IntPtr device)
    {
        var hrtfs = new List<string>();

        if (!AL.IsExtensionPresent(device, HrtfExtension))
            return hrtfs;

        // Not AL.GetStringiSOFT, as its marshalled string return frees memory owned by OpenAL Soft
        var getStringi = (delegate* unmanaged[Cdecl]<IntPtr, int, int, IntPtr>)AL.GetProcAddress(device, "alcGetStringiSOFT");

        if (getStringi == null)
            return hrtfs;

        // Querying the count re-enumerates OpenAL Soft's HRTF list, so these indices match what ALC_HRTF_ID_SOFT is resolved against
        var count = AL.GetIntegerALC(device, AL.ALC_NUM_HRTF_SPECIFIERS_SOFT);

        for (int i = 0; i < count; i++)
        {
            var name = Marshal.PtrToStringUTF8(getStringi(device, AL.ALC_HRTF_SPECIFIER_SOFT, i));

            if (name != null)
                hrtfs.Add(name);
        }

        return hrtfs;
    }

    // Index into the device's current HRTF list for ALC_HRTF_ID_SOFT, or -1 for OpenAL Soft's default
    static int FindHrtfIndex()
    {
        if (string.IsNullOrEmpty(_hrtfName))
            return -1;

        var index = GetHrtfList(ALDevice.handle).IndexOf(_hrtfName);

        if (index < 0)
            LogWarning($"HRTF '{_hrtfName}' was not found, using the default HRTF instead");

        return index;
    }

    // Unlike ALContext.GetAttribs(), this always passes ALC_HRTF_SOFT, so turning HRTF off actually disables it rather than leaving it up to OpenAL Soft's headphone detection
    static int[] BuildAttribs()
    {
        List<int> attribs =
        [
            AL.ALC_FREQUENCY, _sampleRate,
            AL.ALC_MAX_AUXILIARY_SENDS, _maximumAuxiliarySends,
            AL.ALC_MONO_SOURCES, _maximumMonoSources,
            AL.ALC_STEREO_SOURCES, _maximumStereoSources,
        ];

        if (ALDevice.HasExtension(HrtfExtension))
        {
            attribs.Add(AL.ALC_HRTF_SOFT);
            attribs.Add(_hrtfEnabled ? 1 : 0);

            var hrtfIndex = FindHrtfIndex();

            if (hrtfIndex >= 0)
            {
                attribs.Add(AL.ALC_HRTF_ID_SOFT);
                attribs.Add(hrtfIndex);
            }
        }

        attribs.Add(0);
        attribs.Add(0);

        return attribs.ToArray();
    }

    // Applies the current attributes (HRTF etc.) to the open device. alcResetDeviceSOFT keeps every AL object valid, the full recreation fallback doesn't
    static bool ResetDevice()
    {
        // Settings are applied when the device is created
        if (!Initialised)
            return true;

        if (ALDevice.HasExtension(HrtfExtension))
        {
            if (AL.ResetDeviceSOFT(ALDevice.handle, BuildAttribs()))
                return true;

            LogWarning($"Failed to reset the OpenAL device (ALC error {ALDevice.GetErrorALC()}) - falling back to full device recreation");
        }

        RecreateDeviceFully();
        return Initialised;
    }

    // OpenAL Soft searches ALSOFT_LOCAL_PATH for .mhr files every time it enumerates HRTFs, so pointing it at the project's HRTF folder makes those files show up alongside its built-in HRTF
    static void PrepareHrtfFolder()
    {
        if (_hrtfFolderPrepared)
            return;

        _hrtfFolderPrepared = true;

        // Don't override a path the user set themselves
        if (System.Environment.GetEnvironmentVariable(LocalPathVariable) != null)
            return;

        var folder = ProjectSettings.GetSetting("audio/vaudio/hrtf_folder", "res://hrtf").AsString();

        if (string.IsNullOrEmpty(folder))
            return;

        // Outside of exported builds the folder is a real directory, so OpenAL Soft can search it directly - even if it's created or filled after startup
        if (!OS.HasFeature("template"))
        {
            SetNativeEnvironmentVariable(LocalPathVariable, ProjectSettings.GlobalizePath(folder));
            return;
        }

        if (!DirAccess.DirExistsAbsolute(folder))
            return;

        var mhrFiles = new List<string>();

        foreach (var file in DirAccess.GetFilesAt(folder))
        {
            if (file.GetExtension().ToLowerInvariant() == "mhr")
                mhrFiles.Add(file);
        }

        if (mhrFiles.Count == 0)
            return;

        // OpenAL Soft can't open files inside an exported .pck, so copy them out to a real folder first
        var localPath = "user://vaudio/hrtf";
        DirAccess.MakeDirRecursiveAbsolute(localPath);

        // Clear out HRTFs left behind by older builds of this game
        foreach (var file in DirAccess.GetFilesAt(localPath))
            DirAccess.RemoveAbsolute(localPath.PathJoin(file));

        foreach (var file in mhrFiles)
        {
            if (DirAccess.CopyAbsolute(folder.PathJoin(file), localPath.PathJoin(file)) != Error.Ok)
                LogWarning($"Failed to copy HRTF file '{file}' to {localPath}");
        }

        SetNativeEnvironmentVariable(LocalPathVariable, ProjectSettings.GlobalizePath(localPath));
    }

    // System.Environment.SetEnvironmentVariable isn't enough: soft_oal.dll reads the environment through the UCRT's own copy on Windows, and .NET only keeps a managed copy on Linux/macOS
    static void SetNativeEnvironmentVariable(string name, string value)
    {
        System.Environment.SetEnvironmentVariable(name, value);

        if (OperatingSystem.IsWindows())
            _wputenv_s(name, value);
        else
            setenv(name, value, 1);
    }

    [DllImport("ucrtbase.dll", CharSet = CharSet.Unicode)]
    static extern int _wputenv_s(string name, string value);

    [DllImport("libc")]
    static extern int setenv([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int overwrite);

    static void RefreshHrtfList()
    {
        PrepareHrtfFolder();

        List<string> hrtfs;

        if (Initialised)
        {
            hrtfs = GetHrtfList(ALDevice.handle);
        }
        else
        {
            // The editor never opens a device for playback, so open one briefly just to list HRTFs
            var device = AL.OpenDevice(null);
            hrtfs = device == IntPtr.Zero ? [] : GetHrtfList(device);

            if (device != IntPtr.Zero)
                AL.CloseDevice(device);
        }

        var options = new List<string> { DefaultHrtfLabel };
        options.AddRange(hrtfs);

        // Keep a selected HRTF that isn't installed on this machine in the list, rather than the dropdown silently showing a different entry
        var current = ProjectSettings.GetSetting("audio/vaudio/hrtf", DefaultHrtfLabel).AsString();

        if (!string.IsNullOrEmpty(current) && !options.Contains(current))
            options.Add(current);

        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", "audio/vaudio/hrtf" },
            { "type", (int)Variant.Type.String },
            { "hint", (int)PropertyHint.Enum },
            { "hint_string", string.Join(",", options) }
        });
    }
}
