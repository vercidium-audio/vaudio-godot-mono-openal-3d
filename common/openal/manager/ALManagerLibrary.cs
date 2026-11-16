using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    // openal_soft_bindings dlopens OpenAL by name, which never searches the project root or the exe's folder on Linux/macOS. Loading our copy by full path first makes that later dlopen match it by SONAME
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void PreloadOpenALLibrary()
    {
        if (OperatingSystem.IsWindows())
            return;

        string libName = OperatingSystem.IsMacOS() ? "libopenal.1.dylib" : "libopenal.so.1";
        string exeDir = Path.GetDirectoryName(System.Environment.ProcessPath) ?? "";

        string projectDir = "";
        try
        {
            projectDir = ProjectSettings.GlobalizePath("res://");
        }
        catch (Exception)
        {
            // Godot's API isn't usable yet, fall back to the exe's folder
        }

        List<string> dirs = [projectDir, exeDir, Path.Combine(exeDir, "..", "Frameworks")];

        // plugin.gd copies the lib to the project root after this assembly loads, so a fresh project only has the addon's copy
        string addonsDir = Path.Combine(projectDir, "addons");
        if (projectDir.Length > 0 && Directory.Exists(addonsDir))
        {
            string folder = OperatingSystem.IsMacOS() ? "mac" : RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux";
            foreach (string addon in Directory.GetDirectories(addonsDir))
                dirs.Add(Path.Combine(addon, "bin", folder));
        }

        foreach (string dir in dirs)
        {
            if (dir.Length == 0)
                continue;

            string path = Path.Combine(dir, libName);
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out _))
                return;
        }
    }
}
