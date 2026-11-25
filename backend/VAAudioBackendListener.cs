// Human-reviewed

namespace vaudio_godot_mono_openal;

public static partial class VAAudioBackend
{
    static float _listenerPitch;
    static float _listenerYaw;

    public static Vector3 ListenerPosition
    {
        set => Backend?.SetListenerPosition(value);
    }

    public static Vector3 ListenerVelocity
    {
        set => Backend?.SetListenerVelocity(value);
    }

    public static float ListenerPitch
    {
        get => _listenerPitch;
        set
        {
            _listenerPitch = value;
            UpdateListenerOrientation();
        }
    }

    public static float ListenerYaw
    {
        get => _listenerYaw;
        set
        {
            _listenerYaw = value;
            UpdateListenerOrientation();
        }
    }

    static void UpdateListenerOrientation()
    {
        var forward = Helper.PitchYawToVector3(_listenerPitch, _listenerYaw);
        var up = Helper.PitchYawToVector3(_listenerPitch + MathF.PI / 2, _listenerYaw);

        Backend?.SetListenerOrientation(forward, up);
    }
}
