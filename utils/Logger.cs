namespace vaudio_godot_mono_openal;

internal static class Logger
{
    internal static void Log(string message)
    {
        var prefixed = $"[vaudio-godot-mono-openal-3d] {message}";

        Console.WriteLine(prefixed);
        GD.Print(prefixed);
    }

    internal static void LogWarning(string message)
    {
        var prefixed = $"[vaudio-godot-mono-openal-3d] {message}";

        Console.WriteLine(prefixed);
        GD.PushWarning(prefixed);
    }

    internal static void LogError(string message)
    {
        var prefixed = $"[vaudio-godot-mono-openal-3d] {message}";

        Console.Error.WriteLine(prefixed);
        GD.PushError(prefixed);
    }
}