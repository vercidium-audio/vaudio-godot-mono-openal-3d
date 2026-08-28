namespace vaudio_godot_mono_openal;

public static partial class GlobalHelpers
{
    public static bool IsNaNorInfinity(vaudio.Vector v) => IsNaNorInfinity(v.X) || IsNaNorInfinity(v.Y) || IsNaNorInfinity(v.Z);
}
