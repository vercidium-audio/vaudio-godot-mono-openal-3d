namespace vaudio_godot_mono_openal;

/// <summary>
/// A decoded sound owned by the backend. Created from the shared PCM decode path via <see cref="IAudioBackend.GetOrCreateBuffer"/>.
/// </summary>
public interface IAudioBuffer
{
    /// <summary>Sound duration in milliseconds. 0 until decoding finishes.</summary>
    int DurationMs { get; }

    /// <summary>Block until the backing PCM has decoded and been uploaded to the backend.</summary>
    void WaitForLoad();

    /// <summary>
    /// Try to spawn a playing voice from this buffer. Fails if the buffer is still decoding, or the backend ran out of voices.
    /// </summary>
    bool TryCreateSource(bool spatialised, out IAudioSourceHandle source);

    void Dispose();
}
