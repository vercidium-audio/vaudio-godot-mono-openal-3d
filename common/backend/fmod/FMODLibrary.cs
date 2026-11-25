// AI-created

using System.IO;
using System.Runtime.InteropServices;

namespace vaudio_godot_mono_openal;

// Finds the user-supplied FMOD Core library. fmod.cs's DllImports bind by name to a module that's already loaded, so loading it here by full path decides which copy they use, without taking this assembly's DllImportResolver
internal static class FMODLibrary
{
    // The name fmod.cs's DllImport("fmod") resolves to
    static string FileName => OperatingSystem.IsWindows() ? "fmod.dll" : OperatingSystem.IsMacOS() ? "libfmod.dylib" : "libfmod.so";

    static bool _loaded;

    // Returns where FMOD was loaded from, or null with the folders that were searched in error
    internal static string Load(string libraryPathSetting, out string error)
    {
        error = null;

        // Another FMOD integration (or an earlier call) already loaded it, so share that runtime
        if (IsAlreadyLoaded())
        {
            _loaded = true;
            return "the already loaded module";
        }

        List<string> searched = [];

        foreach (string path in CandidatePaths(libraryPathSetting))
        {
            searched.Add(path);

            if (File.Exists(path) && NativeLibrary.TryLoad(path, out _))
            {
                _loaded = true;
                return path;
            }
        }

        // The OS search (PATH etc.)
        if (NativeLibrary.TryLoad(FileName, out _))
        {
            _loaded = true;
            return $"the system search path ({FileName})";
        }

        error = $"Failed to load the FMOD library {FileName}, so the FMOD backend can't play audio. FMOD isn't shipped with vaudio: copy {FileName} from the FMOD Engine (FMOD Studio API) 2.03.x install into the project folder or beside the game's executable, or set audio/vaudio/fmod_library_path. Searched: {string.Join(", ", searched)}";
        return null;
    }

    static IEnumerable<string> CandidatePaths(string libraryPathSetting)
    {
        // The project folder, when running from the editor or `godot --path`
        if (!OS.HasFeature("template"))
            yield return Path.Combine(ProjectSettings.GlobalizePath("res://"), FileName);

        yield return Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()) ?? "", FileName);

        if (!string.IsNullOrEmpty(libraryPathSetting))
        {
            // Either the library itself or the folder holding it
            string path = ProjectSettings.GlobalizePath(libraryPathSetting);
            yield return Directory.Exists(path) ? Path.Combine(path, FileName) : path;
        }
    }

    static bool IsAlreadyLoaded()
    {
        if (_loaded)
            return true;

        return OperatingSystem.IsWindows() && GetModuleHandleW(FileName) != IntPtr.Zero;
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandleW(string moduleName);
}
