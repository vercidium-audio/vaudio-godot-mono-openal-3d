namespace vaudio_godot_mono_openal_3d;

public static unsafe partial class ALManager
{
    public static void SetListenerPosition(Vector3 position) => AL.Listenerfv(AL.AL_POSITION, [position.X, position.Y, position.Z]);
    public static void SetListenerVelocity(Vector3 velocity) => AL.Listenerfv(AL.AL_VELOCITY, [velocity.X, velocity.Y, velocity.Z]);

    public static void SetListenerPitch(float pitch)
    {
        var orientation = Helper.PitchYawToVector3(pitch, _listenerYaw);
        var up = Helper.PitchYawToVector3(pitch + MathF.PI / 2, _listenerYaw);

        AL.Listenerfv(AL.AL_ORIENTATION, [orientation.X, orientation.Y, orientation.Z, up.X, up.Y, up.Z]);

        _listenerPitch = pitch;
    }
    public static void SetListenerYaw(float yaw)
    {
        var orientation = Helper.PitchYawToVector3(_listenerPitch, yaw);
        var up = Helper.PitchYawToVector3(_listenerPitch + MathF.PI / 2, yaw);

        AL.Listenerfv(AL.AL_ORIENTATION, [orientation.X, orientation.Y, orientation.Z, up.X, up.Y, up.Z]);

        _listenerYaw = yaw;
    }

    public static void SetListenerGain(float gain) => AL.Listenerf(AL.AL_GAIN, gain);
    public static void SetDistanceModel(ALDistanceModel model) => AL.DistanceModel((int)model);
    public static void SetMetersPerUnit(float metersPerUnit) => AL.Listenerf(AL.AL_METERS_PER_UNIT, metersPerUnit);
    public static void SetSpeedOfSound(float speedOfSound) => AL.SpeedOfSound(speedOfSound);
    public static void SetReverbOnly(bool value) => _reverbOnly = value;

    // Runtime device switching, reusing whichever max_reverb_sends/sample_rate/hrtf_enabled were
    // read from Project Settings at initialize() time - matches native's bound
    // ALManager::set_output_device(), the only one of the four audio/vaudio/* settings it exposes
    // for runtime changes.
    public static void SetOutputDevice(string deviceName)
    {
        _outputDeviceName = deviceName;
        RecreateDevice();
    }

    public static void UpdateListener(Vector3 position, float pitch, float yaw)
    {
        var cameraVel = Vector3.Zero;
        var orientation = Helper.PitchYawToVector3(pitch, yaw);

        // Up vector MUST be perpendicular to look direction, else spatialisation gets distorted
        var up = Helper.PitchYawToVector3(pitch + MathF.PI / 2, yaw);

        AL.Listenerfv(AL.AL_POSITION, [position.X, position.Y, position.Z]);
        AL.Listenerfv(AL.AL_VELOCITY, [cameraVel.X, cameraVel.Y, cameraVel.Z]);
        AL.Listenerfv(AL.AL_ORIENTATION, [orientation.X, orientation.Y, orientation.Z, up.X, up.Y, up.Z]);
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
