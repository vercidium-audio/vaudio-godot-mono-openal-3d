// AI-created

namespace vaudio_godot_mono_openal;

public static partial class VAAudioBackend
{
    static Dictionary<AudioStream, VADecodedSound> DecodedStreams = [];

    // Decodes each AudioStream once, on a worker thread, and keeps it for every player that uses it
    public static VADecodedSound GetOrCreateSound(AudioStream stream)
    {
        if (DecodedStreams.TryGetValue(stream, out var sound))
            return sound;

        sound = new VADecodedSound(Backend, stream);
        DecodedStreams[stream] = sound;
        return sound;
    }

    /// <summary>
    /// Try to create a voice for the stream. Fails while the stream is still decoding, or when the backend is out of voices
    /// </summary>
    public static bool TryCreateVoice(AudioStream stream, bool spatialised, out VAVoice voice)
    {
        if (stream == null || Backend == null)
        {
            voice = null;
            return false;
        }

        return GetOrCreateSound(stream).TryCreateVoice(spatialised, out voice);
    }

    /// <summary>
    /// Try to create a stream that plays raw PCM pushed into it. Its voice starts playing straight away. Fails when the backend is out of voices, which it logs
    /// </summary>
    public static bool TryCreateStream(VAPcmFormat format, int sampleRate, out VAStream stream)
    {
        stream = null;
        return Backend != null && Backend.TryCreateStream(format, sampleRate, out stream);
    }

    // Stops the background decodes and forgets every decoded sound. The backend frees their data with the device
    static void CancelLoadingSounds()
    {
        VADecodedSound.CancelLoading = true;

        foreach (var sound in DecodedStreams.Values)
            sound.WaitForTask();

        DecodedStreams.Clear();
        VADecodedSound.CancelLoading = false;
    }
}
