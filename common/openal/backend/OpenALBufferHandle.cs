using System.Threading.Tasks;

using OpenALManagedSource = global::OpenAL.managed.ALSource;

namespace vaudio_godot_mono_openal;

/// <summary>
/// Wraps a decoded <see cref="AudioBuffer"/> as an <see cref="IAudioBuffer"/> and owns the OpenAL buffer object.
/// The AL upload (<c>alGenBuffers</c> + <c>alBufferData</c>) runs on a continuation of the decode task, matching the
/// old <c>AudioBuffer.Load</c> behaviour where the PCM was copied into OpenAL off the main thread. The PCM is freed
/// once uploaded.
/// </summary>
public class OpenALBufferHandle : IAudioBuffer
{
    readonly AudioBuffer buffer;
    readonly ALContext context;
    readonly Task uploadTask;

    uint handle;

    public OpenALBufferHandle(AudioBuffer buffer, ALContext context)
    {
        this.buffer = buffer;
        this.context = context;
        uploadTask = buffer.LoadingTask.ContinueWith(_ => BufferOpenALData(), TaskContinuationOptions.ExecuteSynchronously);
    }

    public int DurationMs => buffer.DurationMs;

    public void WaitForLoad()
    {
        buffer.WaitForLoad();
        uploadTask?.Wait();
    }

    /// <summary>Copy the decoded PCM into an OpenAL buffer. PCM is freed afterwards.</summary>
    unsafe void BufferOpenALData()
    {
        var pcmData = buffer.GetPcm();

        if (pcmData == null || AudioBuffer.CancelLoadingSounds)
            return;

        context.MakeCurrent();

        Debug.Assert(handle == 0);
        handle = AL.GenBuffer();

        // MixAudio always returns interleaved stereo frames regardless of the source stream's
        // own channel count (Godot's mixer upmixes mono internally), so stereo/16-bit is always correct here.
        var format = AL.GetSoundFormat(2, 16);

        fixed (short* shortPtr = pcmData)
        {
            AL.BufferData(handle, format, (nint)shortPtr, pcmData.Length * sizeof(short), buffer.SampleRate);
        }

        buffer.ReleasePcm();
    }

    /// <summary>
    /// Try to create an AL source from this buffer. Fails if the buffer is still loading, or if too many sources
    /// have been created (increase max_mono_sources / max_stereo_sources in project settings).
    /// </summary>
    public bool TryCreateSource(bool spatialised, out IAudioSourceHandle source)
    {
        source = null;

        // Bail if this buffer is still loading, or we failed to initialise OpenAL
        if (handle == 0)
            return false;

        // TODO - this throws when playing too many sounds at once
        var sourceID = AL.GenSource();

        // Out of memory
        if (sourceID == 0)
        {
            LogWarning($"Failed to create source - likely too many sources have been created");
            return false;
        }

        var alSource = new OpenALManagedSource(sourceID);
        alSource.SetBuffer(handle);
        alSource.SetRelative(!spatialised);
        alSource.SetSpatialise(spatialised);

        source = new OpenALSourceHandle(alSource);
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
