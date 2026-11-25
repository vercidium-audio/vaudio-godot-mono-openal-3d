namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    public static bool Initialised => ALDevice != null;

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
        OpenAL.Logger.Error = LogBackendError;

        CreateDeviceAndContext();
    }

    static int _errorCount;

    // How many OpenAL errors have been logged since the game started, so tests can check a run hit no backend errors
    public static int ErrorCount => System.Threading.Volatile.Read(ref _errorCount);

    // Logs a failed OpenAL call and counts it in ErrorCount
    internal static void LogBackendError(string message)
    {
        System.Threading.Interlocked.Increment(ref _errorCount);
        LogError(message);
    }

    public static void Update()
    {
        if (Engine.IsEditorHint() || !Initialised)
            return;

        if (!CheckDeviceConnected())
            return;

        ALContext.Process();
        DisposeFinishedSources();
    }

    static float _masterVolume = 1;
    static ALDistanceModel _distanceModel = ALDistanceModel.InverseDistance;
    static float _metersPerUnit = 1;
    static float _speedOfSound = 343;
    static bool _reverbOnly;

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
