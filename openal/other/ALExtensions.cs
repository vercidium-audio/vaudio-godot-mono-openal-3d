using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal_3d;

public static class ALExtensions
{
    public static void SetPosition(this OpenALSource source, Vector3 v)  => AL.Sourcefv(source.ID, AL.AL_POSITION, [v.X, v.Y, v.Z]);
    public static void SetVelocity(this OpenALSource source, Vector3 v)  => AL.Sourcefv(source.ID, AL.AL_VELOCITY, [v.X, v.Y, v.Z]);
    public static void SetDirection(this OpenALSource source, Vector3 v) => AL.Sourcefv(source.ID, AL.AL_DIRECTION, [v.X, v.Y, v.Z]);
}