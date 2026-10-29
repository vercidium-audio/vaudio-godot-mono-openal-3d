namespace vaudio_godot_mono_openal;

internal static partial class Logger
{
    internal static void Log(string message)
    {
        var prefixed = $"{Prefix} {message}";

        Console.WriteLine(prefixed);
        GD.Print(prefixed);
    }

    internal static void LogWarning(string message)
    {
        var prefixed = $"{Prefix} {message}";

        Console.WriteLine(prefixed);
        GD.PushWarning(prefixed);
    }

    internal static void LogError(string message)
    {
        var prefixed = $"{Prefix} {message}";

        Console.Error.WriteLine(prefixed);
        GD.PushError(prefixed);
    }

    const string SDKErrorPrefix = "[Error] ";
    const string SDKWarningPrefix = "[Warning] ";

    // The SDK prefixes its log lines with their severity, so route them to push_error / push_warning and they show in Godot's Errors tab
    internal static void LogSDK(string message)
    {
        if (message.StartsWith(SDKErrorPrefix, StringComparison.Ordinal))
            LogError(message[SDKErrorPrefix.Length..]);
        else if (message.StartsWith(SDKWarningPrefix, StringComparison.Ordinal))
            LogWarning(message[SDKWarningPrefix.Length..]);
        else
            Log(message);
    }
}
