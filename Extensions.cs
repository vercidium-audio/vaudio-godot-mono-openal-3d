global using static vaudio_godot_mono_openal_3d.Extensions;
global using static vaudio_godot_mono_openal_3d.GlobalHelpers;

using godot_mono_openal;

namespace vaudio_godot_mono_openal_3d;

internal static class Extensions
{
    public static vaudio.Color ToVAudio(Godot.Color c) => new(c.R, c.G, c.B, c.A);

    public static vaudio.Vector ToVAudio(Vector3 v) => new(v.X, v.Y, v.Z);
    public static Vector3 FromVAudio(vaudio.Vector v) => new(v.X, v.Y, v.Z);

    public static vaudio.Matrix ToVAudio(Transform3D globalTransform)
    {
        var basis = globalTransform.Basis;
        var origin = globalTransform.Origin;

        // Both Godot's Basis and vaudio.Matrix4F are column-major
        return new vaudio.Matrix(
            basis.X.X, basis.X.Y, basis.X.Z, 0f,
            basis.Y.X, basis.Y.Y, basis.Y.Z, 0f,
            basis.Z.X, basis.Z.Y, basis.Z.Z, 0f,
            origin.X, origin.Y, origin.Z, 1f
        );
    }

    public static bool GodotOpenALEnabled => ALManager.Instance != null;

    public static void RegisterDeviceRecreatedCallback(Action callback) => ALManager.Instance?.RegisterDeviceRecreatedCallback(callback);
    public static void RegisterDeviceDestroyedCallback(Action callback) => ALManager.Instance?.RegisterDeviceDestroyedCallback(callback);
}
