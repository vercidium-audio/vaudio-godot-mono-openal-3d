namespace vaudio_godot_mono_openal;

public static class Helper
{
    // Matches Godot's -Z forward for a node rotated by (pitch, yaw, 0) in YXZ order. pitch + PI/2 gives the node's up vector
    public static Vector3 PitchYawToVector3(float pitch, float yaw)
    {
        Vector3 direction = new()
        {
            X = -Mathf.Cos(pitch) * Mathf.Sin(yaw),
            Y = Mathf.Sin(pitch),
            Z = -Mathf.Cos(pitch) * Mathf.Cos(yaw)
        };

        return direction.Normalized();
    }
}
