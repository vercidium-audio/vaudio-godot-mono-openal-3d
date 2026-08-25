using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal_3d;

// A sound source relative to the listener with zero offset, e.g. footsteps,
// ambience, music. Always AL_SOURCE_RELATIVE with a pinned origin position -
// used to be a toggleable `Relative` bool on ALSource3D, which caused
// mispositioned/panned audio since it left the node's GlobalPosition being
// synced onto a relative source every frame
[Tool]
public partial class ALSourceRelative : ALSource
{
    protected override void ConfigureSource(OpenALSource source)
    {
        source.SetRelative(true);
        source.SetPosition(Vector3.Zero);
    }
}
