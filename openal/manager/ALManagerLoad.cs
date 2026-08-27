namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    // One ALBuffer per unique AudioStream resource, decoded on first use and reused by every
    // ALSource3D that references the same stream. Keyed by the AudioStream instance rather than
    // a file path since a stream can be created at runtime without a resource_path.
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
