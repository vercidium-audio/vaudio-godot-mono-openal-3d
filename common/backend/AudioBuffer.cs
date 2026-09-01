using System.Threading.Tasks;

namespace vaudio_godot_mono_openal;

/// <summary>
/// Backend-agnostic decoded PCM for one <see cref="AudioStream"/>. Decoding runs on a background task
/// kicked off in the constructor; the result is interleaved stereo 16-bit PCM at <see cref="SampleRate"/>.
/// A backend wraps this (e.g. <c>OpenALBufferHandle</c>) and uploads <see cref="GetPcm"/> into its own buffer object.
/// </summary>
public class AudioBuffer
{
    /// <summary>Set by the OpenAL device teardown to make in-flight decode tasks bail early.</summary>
    public static bool CancelLoadingSounds;

    const int MixChunkFrames = 8192;

    readonly AudioStream stream;
    readonly Task loadingTask;

    short[] pcmData;

    /// <summary>Mix rate the PCM was decoded at, in Hz. 0 until decoding finishes.</summary>
    public int SampleRate { get; private set; }

    /// <summary>Sound duration in milliseconds. 0 until decoding finishes.</summary>
    public int DurationMs { get; private set; }

    /// <summary>True once decoding produced PCM and it hasn't been released yet.</summary>
    public bool PcmReady => pcmData != null;

    public AudioBuffer(AudioStream stream)
    {
        this.stream = stream;
        loadingTask = Task.Run(Load);
    }

    /// <summary>The decode task, so a backend can chain its upload after it.</summary>
    public Task LoadingTask => loadingTask;

    /// <summary>Block until the background decode finishes.</summary>
    public void WaitForLoad() => loadingTask?.Wait();

    /// <summary>The decoded interleaved stereo 16-bit PCM, or null if decoding failed / was cancelled / already released.</summary>
    public short[] GetPcm() => pcmData;

    /// <summary>Free the PCM once a backend has copied it into its own buffer.</summary>
    public void ReleasePcm() => pcmData = null;

    static short FloatToPCM16(float sample)
    {
        var clamped = Math.Clamp(sample, -1.0f, 1.0f);
        return (short)(clamped * short.MaxValue);
    }

    void Load()
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

        SampleRate = (int)mixRate;
        DurationMs = (int)(framesPulled * 1000 / mixRate);
        pcmData = [.. pending];
    }
}
