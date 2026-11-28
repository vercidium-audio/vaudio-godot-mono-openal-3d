// AI-created

using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace vaudio_godot_mono_openal;

internal static class OpenALLibrary
{
    // openal_soft_bindings loads OpenAL by name, which never searches the addon's bin folder (and on Linux/macOS not the exe's folder either). Loading our copy by full path first makes that later load reuse it: by SONAME on Linux/macOS, by module name on Windows
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void PreloadOpenALLibrary()
    {
        string libName = OperatingSystem.IsWindows() ? "soft_oal.dll" : OperatingSystem.IsMacOS() ? "libopenal.1.dylib" : "libopenal.so.1";
        string exeDir = Path.GetDirectoryName(System.Environment.ProcessPath) ?? "";

        // No Godot API here (see VAAddonBin). The editor's --path chdirs into the project, so the current directory is the project root
        string projectDir = Directory.GetCurrentDirectory();

        // Exports have OpenAL beside the exe. In the editor the addon's copy wins over one left in the project root by older versions of the plugin, so upgrading the plugin upgrades OpenAL
        List<string> dirs = [exeDir, Path.Combine(exeDir, "..", "Frameworks"), .. VAAddonBin.PlatformDirs(), projectDir];

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
