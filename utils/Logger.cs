namespace vaudio_godot_mono_openal_3d;

internal static class Logger
{
    internal static void Log(string message)
    {
        var prefixed = $"[godot-mono-openal] {message}";

        Console.WriteLine(prefixed);
        GD.Print(prefixed);
    }

    internal static void LogWarning(string message)
    {
        var prefixed = $"[godot-mono-openal] {message}";

        Console.WriteLine(prefixed);
        GD.PushWarning(prefixed);
    }

    internal static void LogError(string message)
    {
        var prefixed = $"[godot-mono-openal] {message}";

        Console.Error.WriteLine(prefixed);
        GD.PushError(prefixed);
    }
}