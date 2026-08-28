namespace vaudio_godot_mono_openal;

public static class GlobalHelpers
{
    // Helpers
    public static bool IsNaNorInfinity(float v) => float.IsNaN(v) || float.IsInfinity(v);
    public static bool IsNaNorInfinity(vaudio.Vector v) => IsNaNorInfinity(v.X) || IsNaNorInfinity(v.Y) || IsNaNorInfinity(v.Z);

    public static float Lerp(float current, float target, float lerp) => current + (target - current) * lerp;
}
