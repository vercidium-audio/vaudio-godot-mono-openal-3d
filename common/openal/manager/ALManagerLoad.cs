namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    static Dictionary<AudioStream, OpenALBufferHandle> DecodedStreams = [];

    public static OpenALBufferHandle GetOrCreateBuffer(AudioStream stream)
    {
        if (DecodedStreams.TryGetValue(stream, out var handle))
            return handle;

        handle = new OpenALBufferHandle(new AudioBuffer(stream), ALContext);
        DecodedStreams[stream] = handle;
        return handle;
    }
}
