// AI-created

using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace vaudio_godot_mono_openal;

// The addon's per-platform binaries live in addons/{addon}/bin/{folder}, named like the Vercidium Audio SDK package's platform folders
internal static class VAAddonBin
{
    internal static string PlatformFolder()
    {
        bool arm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        if (OperatingSystem.IsWindows())
            return arm64 ? "windows-arm64" : "windows";
        if (OperatingSystem.IsMacOS())
            return "mac";
        return arm64 ? "linux-arm64" : "linux";
    }

    // No Godot API here: in exported games this assembly loads before Godot's interop is initialised, and calling into it segfaults. The editor's --path chdirs into the project, so the current directory is the project root
    internal static IEnumerable<string> PlatformDirs()
    {
        string addonsDir = Path.Combine(Directory.GetCurrentDirectory(), "addons");
        if (!Directory.Exists(addonsDir))
            yield break;

        string folder = PlatformFolder();
        foreach (string addon in Directory.GetDirectories(addonsDir))
            yield return Path.Combine(addon, "bin", folder);
    }

    // Godot loads vaudio.dll from memory, so the dev SDK can't find its debug window beside it. Only set when the dev window is there, so production installs and exports behave as before
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void SetDebugWindowDirectory()
    {
        string exeName = OperatingSystem.IsWindows() ? "vaudio-debug-window.exe" : "vaudio-debug-window";

        foreach (string dir in PlatformDirs())
        {
            if (File.Exists(Path.Combine(dir, exeName)))
            {
                AppContext.SetData("vaudio.DebugWindowDirectory", Path.GetFullPath(dir));
                return;
            }
        }
    }
}
