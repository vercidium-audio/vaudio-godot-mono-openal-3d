namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    static Vector3 _listenerPosition;
    static Vector3 _listenerVelocity;
    static float _listenerPitch;
    static float _listenerYaw;

    public static Vector3 ListenerPosition
    {
        get => _listenerPosition;
        set => UpdateProperty(ref _listenerPosition, value, SetListenerPosition);
    }

    public static Vector3 ListenerVelocity
    {
        get => _listenerVelocity;
        set => UpdateProperty(ref _listenerVelocity, value, SetListenerVelocity);
    }

    public static float ListenerPitch
    {
        get => _listenerPitch;
        set => UpdateProperty(ref _listenerPitch, value, SetListenerPitch);
    }

    public static float ListenerYaw
    {
        get => _listenerYaw;
        set => UpdateProperty(ref _listenerYaw, value, SetListenerYaw);
    }

    static void SetListenerPosition(Vector3 position) => AL.Listenerfv(AL.AL_POSITION, [position.X, position.Y, position.Z]);
    static void SetListenerVelocity(Vector3 velocity) => AL.Listenerfv(AL.AL_VELOCITY, [velocity.X, velocity.Y, velocity.Z]);

    static void SetListenerPitch(float pitch)
    {
        var orientation = Helper.PitchYawToVector3(pitch, _listenerYaw);
        var up = Helper.PitchYawToVector3(pitch + MathF.PI / 2, _listenerYaw);

        AL.Listenerfv(AL.AL_ORIENTATION, [orientation.X, orientation.Y, orientation.Z, up.X, up.Y, up.Z]);
    }

    static void SetListenerYaw(float yaw)
    {
        var orientation = Helper.PitchYawToVector3(_listenerPitch, yaw);
        var up = Helper.PitchYawToVector3(_listenerPitch + MathF.PI / 2, yaw);

        AL.Listenerfv(AL.AL_ORIENTATION, [orientation.X, orientation.Y, orientation.Z, up.X, up.Y, up.Z]);
    }
}
