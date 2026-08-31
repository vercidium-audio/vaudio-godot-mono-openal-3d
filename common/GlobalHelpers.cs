namespace vaudio_godot_mono_openal;

public static partial class GlobalHelpers
{
    public static bool IsNaNorInfinity(float v) => float.IsNaN(v) || float.IsInfinity(v);

    public static float Lerp(float current, float target, float lerp) => current + (target - current) * lerp;
}
