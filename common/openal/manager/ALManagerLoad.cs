namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    static Dictionary<AudioStream, ALBuffer> DecodedStreams = [];

    public static ALBuffer GetOrCreateBuffer(AudioStream stream)
    {
        if (DecodedStreams.TryGetValue(stream, out var buffer))
            return buffer;

        buffer = new ALBuffer(ALContext, stream);
        DecodedStreams[stream] = buffer;
        return buffer;
    }
}
