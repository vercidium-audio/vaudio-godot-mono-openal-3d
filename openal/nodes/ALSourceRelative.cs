using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

/// <summary>
/// A sound source relative to the listener, never muffled, reuses listener reverb.
/// </summary>
[Tool]
public partial class ALSourceRelative : ALSource
{
    protected override void ConfigureSource(OpenALSource source)
    {
        source.SetRelative(true);
        source.SetPosition(Vector3.Zero);
    }
}
