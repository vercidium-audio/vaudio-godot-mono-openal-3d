namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    public static bool Initialised => ALDevice != null;

    // Idempotent - a no-op once already Initialised, or in the editor (CreateDeviceAndContext()
    // opens a real OpenAL device, which should only happen at game runtime). Every VA*/AL* node
    // calls this at the top of its own _EnterTree() so OpenAL is ready by the time any of them
    // need it, regardless of scene-tree order or which node happens to enter first.
    public static void Ensure()
    {
        if (Initialised || Engine.IsEditorHint())
            return;

        // Log to both - in case we're launched from vs2026 or from the Godot Editor
        OpenAL.Logger.Log = (message) =>
        {
            Console.WriteLine(message);
            GD.Print(message);
        };
        OpenAL.Logger.Error = (message) =>
        {
            Console.Error.WriteLine(message);
            GD.PushError(message);
        };

        CreateDeviceAndContext();
    }

    // Called once per frame by VAWorld._Process (main/VAWorldGodot.cs) - matches how VAWorld
    // already calls world.Update() for the raytracer every frame. No longer a Node._Process
    // override, since ALManager is no longer a Node.
    public static void Update()
    {
        if (Engine.IsEditorHint() || !Initialised)
            return;

        ALContext.Process();
        DisposeFinishedSources();
    }

    static float _masterVolume = 1;
    static ALDistanceModel _distanceModel = ALDistanceModel.InverseDistance;
    static float _metersPerUnit = 1;
    static float _speedOfSound = 343;
    static bool _reverbOnly;

    // MasterVolume/DistanceModel/MetersPerUnit/SpeedOfSound/ReverbOnly and the listener
    // position/velocity/rotation props (per-addon, ALManagerListener.cs) are runtime-API-only
    // (no inspector UI), matching native's shape - call the Set* methods directly from code.

    public static float MasterVolume
    {
        get => _masterVolume;
        set => UpdateProperty(ref _masterVolume, MathF.Max(0, value), SetListenerGain);
    }

    public static ALDistanceModel DistanceModel
    {
        get => _distanceModel;
        set => UpdateProperty(ref _distanceModel, value, SetDistanceModel);
    }

    public static float MetersPerUnit
    {
        get => _metersPerUnit;
        set => UpdateProperty(ref _metersPerUnit, MathF.Max(0, value), SetMetersPerUnit);
    }

    public static float SpeedOfSound
    {
        get => _speedOfSound;
        set => UpdateProperty(ref _speedOfSound, MathF.Max(0, value), SetSpeedOfSound);
    }

    public static bool ReverbOnly
    {
        get => _reverbOnly;
        set => UpdateProperty(ref _reverbOnly, value, SetReverbOnly);
    }

    // MaximumAuxiliarySends, SampleRate, HRTFEnabled, MaximumMonoSources and MaximumStereoSources
    // are read once from Project Settings (audio/vaudio/*) during CreateDeviceAndContext() - see
    // ALManagerDevice.cs - matching native's read_settings_from_project_settings(); they're not
    // settable at runtime there either, since ALManager's only bound device-switching method
    // (set_output_device) reuses whatever these were at initialize() time.

    // Read once from Project Settings (audio/vaudio/output_device) during
    // CreateDeviceAndContext() - see ALManagerDevice.cs - no longer an inspector-editable
    // property, matching native's output device now only being configurable via Project
    // Settings (or ALManager.SetOutputDevice at runtime).
    static string _outputDeviceName;

    static void UpdateProperty<T>(ref T field, T value, Action<T> updateAction = null) where T : struct
    {
        if (!field.Equals(value))
        {
            field = value;
            updateAction?.Invoke(value);
        }
    }
}
