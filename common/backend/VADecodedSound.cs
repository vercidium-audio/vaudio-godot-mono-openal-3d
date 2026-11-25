// AI-created

using System.Threading.Tasks;

namespace vaudio_godot_mono_openal;

// Decodes a Godot AudioStream to PCM on a worker thread, then hands it to the backend
public class VADecodedSound
{
    public static volatile bool CancelLoading;
    const int MixChunkFrames = 8192;

    readonly AudioStream stream;
    readonly Task loadingTask;
    VASound sound;

    public VADecodedSound(VAAudioBackendBase backend, AudioStream stream)
    {
        this.stream = stream;
        loadingTask = Task.Run(() => Load(backend));
    }

    static short FloatToPCM16(float sample)
    {
        var clamped = Math.Clamp(sample, -1.0f, 1.0f);
        return (short)(clamped * short.MaxValue);
    }

    void Load(VAAudioBackendBase backend)
    {
        if (CancelLoading)
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
            if (CancelLoading)
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

        // Bail if we're changing audio devices
        if (CancelLoading)
            return;

        // MixAudio always returns interleaved stereo frames regardless of the source stream's own channel count (Godot's mixer upmixes mono internally)
        System.Threading.Volatile.Write(ref sound, backend.CreateSound([.. pending], 2, (int)mixRate));
    }

    public void WaitForTask()
    {
        loadingTask?.Wait();
    }

    /// <summary>
    /// Try to create a voice from this sound. Fails while it's still decoding, or when the backend is out of voices
    /// </summary>
    public bool TryCreateVoice(bool spatialised, out VAVoice voice)
    {
        var decoded = System.Threading.Volatile.Read(ref sound);

        if (decoded == null)
        {
            voice = null;
            return false;
        }

        return decoded.TryCreateVoice(spatialised, out voice);
    }
}
