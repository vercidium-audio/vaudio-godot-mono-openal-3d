namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    public static void SetListenerGain(float gain) => AL.Listenerf(AL.AL_GAIN, gain);
    public static void SetDistanceModel(ALDistanceModel model) => AL.DistanceModel((int)model);
    public static void SetMetersPerUnit(float metersPerUnit) => AL.Listenerf(AL.AL_METERS_PER_UNIT, metersPerUnit);
    public static void SetSpeedOfSound(float speedOfSound) => AL.SpeedOfSound(speedOfSound);
    public static void SetReverbOnly(bool value) => _reverbOnly = value;

    public static void SetOutputDevice(string deviceName)
    {
        _outputDeviceName = deviceName;
        RecreateDevice();
    }

    static void DisposeFinishedSources()
    {
        for (int i = ActiveSources.Count - 1; i >= 0; i--)
        {
            var s = ActiveSources[i];

            if (s.Finished())
            {
                s.Dispose();
                ActiveSources.RemoveAt(i);
            }
        }
    }
}
