using System.Linq;

namespace vaudio_godot_mono_openal;

/// <summary>
/// The audio backends compiled into this assembly. Each backend folder registers itself from a [ModuleInitializer], so core code never names a backend type
/// </summary>
public static class VAAudioBackendRegistry
{
    public const string SettingName = "audio/vaudio/backend";
    public const string DefaultBackend = "OpenAL";

    static readonly Dictionary<string, Func<VAAudioBackendBase>> factories = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(string name, Func<VAAudioBackendBase> factory) => factories[name] = factory;

    public static IEnumerable<string> Names => factories.Keys;

    // Creates the backend chosen by audio/vaudio/backend, falling back to the default (or any registered one) when it isn't compiled in
    internal static VAAudioBackendBase CreateSelected()
    {
        var selected = ProjectSettings.GetSetting(SettingName, DefaultBackend).AsString();

        if (factories.TryGetValue(selected, out var factory))
            return factory();

        string fallback = factories.ContainsKey(DefaultBackend) ? DefaultBackend : factories.Keys.FirstOrDefault();

        if (fallback == null)
        {
            LogError($"No audio backends are available, so no audio will play. The '{selected}' backend selected in the {SettingName} project setting isn't compiled in");
            return null;
        }

        var backend = factories[fallback]();
        backend.LogError($"The '{selected}' audio backend selected in the {SettingName} project setting is not available, so the {fallback} backend is used instead");
        return backend;
    }
}
