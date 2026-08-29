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
        OpenAL.Logger.Error = (message) =>
        {
            Console.Error.WriteLine(message);
            GD.PushError(message);
        };

        CreateDeviceAndContext();
    }

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
