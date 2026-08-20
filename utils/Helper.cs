namespace vaudio_godot_mono_openal_3d;

public static class Helper
{
    public static Vector3 PitchYawToVector3(float pitch, float yaw)
    {
        Vector3 direction = new()
        {
            X = Mathf.Cos(pitch) * Mathf.Sin(yaw),
            Y = -Mathf.Sin(pitch),
            Z = Mathf.Cos(pitch) * Mathf.Cos(yaw)
        };

        return direction.Normalized();
    }
}
