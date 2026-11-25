// AI-created

using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

// An AL buffer holding a decoded sound
sealed class OpenALSound(OpenALBackend backend, uint handle) : VASound
{
    uint handle = handle;

    public override float Length => OpenALVoice.BufferLength(handle);

    public override bool TryCreateVoice(bool spatialised, out VAVoice voice)
    {
        voice = null;

        if (handle == 0)
            return false;

        var id = backend.GenSource();

        if (id == 0)
            return false;

        var source = new OpenALSource(id);
        source.SetBuffer(handle);
        source.SetRelative(!spatialised);
        source.SetSpatialise(spatialised);

        voice = new OpenALVoice(source);
        return true;
    }

    public override void Dispose()
    {
        if (handle == 0)
            return;

        AL.DeleteBuffer(handle);
        handle = 0;
    }
}
