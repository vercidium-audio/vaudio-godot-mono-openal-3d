using OpenALManagedSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

/// <summary>
/// Wraps an <see cref="ALBuffer"/> as an <see cref="IAudioBuffer"/>.
/// </summary>
public class OpenALBufferHandle : IAudioBuffer
{
    readonly ALBuffer buffer;

    public OpenALBufferHandle(ALBuffer buffer) => this.buffer = buffer;

    public int DurationMs => buffer.Duration;

    public void WaitForLoad() => buffer.WaitForTask();

    public bool TryCreateSource(bool spatialised, out IAudioSourceHandle source)
    {
        if (buffer.TryCreateSource(spatialised, out OpenALManagedSource alSource))
        {
            source = new OpenALSourceHandle(alSource);
            return true;
        }

        source = null;
        return false;
    }

    public void Dispose() => buffer.Dispose();
}
