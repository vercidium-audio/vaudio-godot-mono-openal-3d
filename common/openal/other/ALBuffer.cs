using System.Threading.Tasks;

using OpenALSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

public class ALBuffer
{
    public static bool CancelLoadingSounds;
    const int MixChunkFrames = 8192;

    AudioStream stream;
    Task loadingTask;

    short[] pcmData;
    int sampleRate;

    /// <summary>
    /// The duration of the sound in milliseconds
    /// </summary>
    public int Duration { get; private set; }

    public ALBuffer(ALContext context, AudioStream stream)
    {
        this.stream = stream;
        loadingTask = Task.Run(() => Load(context));
    }

    static short FloatToPCM16(float sample)
    {
        var clamped = Math.Clamp(sample, -1.0f, 1.0f);
        return (short)(clamped * short.MaxValue);
    }

    void Load(ALContext context)
    {
        if (CancelLoadingSounds)
            return;

        // Decodes via Godot's own AudioStream/import pipeline
        var playback = stream.InstantiatePlayback();

        if (playback == null)
        {
            LogWarning($"Cannot buffer data for {stream.ResourcePath} as its AudioStream failed to instantiate a playback (unsupported/corrupt file?)");
            return;
        }

        var mixRate = AudioServer.GetMixRate();
        var lengthSeconds = stream.GetLength();

        // Some streams (e.g. procedurally-generated or malformed ones) report a zero/negative length.
        // Fall back to pulling until MixAudio stops returning frames
        long expectedFrames = lengthSeconds > 0.0
            ? (long)(lengthSeconds * mixRate) + MixChunkFrames
            : (long)mixRate * 60 * 10; // 10 minute safety cap

        playback.Start();

        var pending = new List<short>((int)Math.Min(expectedFrames, (long)mixRate * 60) * 2);

        long framesPulled = 0;

        while (framesPulled < expectedFrames)
        {
            if (CancelLoadingSounds)
                return;

            Vector2[] chunk = playback.MixAudio(1.0f, MixChunkFrames);

            if (chunk == null || chunk.Length == 0)
                break;

            foreach (var frame in chunk)
            {
                pending.Add(FloatToPCM16(frame.X));
                pending.Add(FloatToPCM16(frame.Y));
            }

            framesPulled += chunk.Length;

            // MixAudio returning fewer frames than requested means the stream has ended
            if (chunk.Length < MixChunkFrames)
                break;
        }

        playback.Stop();

        if (pending.Count == 0)
        {
            LogWarning($"AudioStream {stream.ResourcePath} decoded to zero frames");
            return;
        }

        pcmData = [.. pending];
        sampleRate = (int)mixRate;
        Duration = (int)(framesPulled * 1000 / mixRate);

        // Bail if we're changing audio devices
        if (CancelLoadingSounds)
            return;

        context.MakeCurrent();
        BufferOpenALData();
    }

    public void WaitForTask()
    {
        loadingTask?.Wait();
    }

    uint handle;

    /// <summary>
    /// Copy PCM data to the OpenAL buffer. PCM data is freed afterwards
    /// </summary>
    public unsafe void BufferOpenALData()
    {
        if (pcmData == null)
            return;

        Debug.Assert(handle == 0);
        handle = AL.GenBuffer();

        // MixAudio always returns interleaved stereo frames regardless of the source stream's
        // own channel count (Godot's mixer upmixes mono internally), so stereo/16-bit is always correct here.
        var format = AL.GetSoundFormat(2, 16);

        fixed (short* shortPtr = pcmData)
        {
            AL.BufferData(handle, format, (nint)shortPtr, pcmData.Length * sizeof(short), sampleRate);
        }

        // Free memory
        pcmData = null;
    }

    /// <summary>
    /// Try to create an AL source from this buffer. Will fail if the buffer is still loading, or if too many sources have been created (increase MaximumMonoSources or MaximumStereoSources in project settings)
    /// </summary>
    public bool TryCreateSource(bool spatialised, out OpenALSource source)
    {
        // Bail if this buffer is still loading, or we failed to initialise OpenAL
        if (handle == 0)
        {
            source = null;
            return false;
        }

        // TODO - this throws when playing too many sounds at once
        var sourceID = AL.GenSource();

        // Out of memory
        if (sourceID == 0)
        {
            LogWarning($"Failed to create source - likely too many sources have been created");
            source = null;
            return false;
        }

        source = new OpenALSource(sourceID);
        source.SetBuffer(handle);
        source.SetRelative(!spatialised);
        source.SetSpatialise(spatialised);

        return true;
    }

    public void Dispose()
    {
        // Already disposed, or loading was cancelled
        if (handle == 0)
            return;

        AL.DeleteBuffer(handle);
        handle = 0;
    }
}
