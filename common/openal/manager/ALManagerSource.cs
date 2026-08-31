using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    public static bool TryCreateSource(AudioStream stream, bool spatialised, out OpenALSource source)
    {
        if (stream == null)
        {
            source = null;
            return false;
        }

        var buffer = GetOrCreateBuffer(stream);
        return buffer.TryCreateSource(spatialised, out source);
    }
}
