// AI-created

using System.Runtime.CompilerServices;

namespace vaudio_godot_mono_openal;

// Audio backend powered by OpenAL Soft
public sealed partial class OpenALBackend : VAAudioBackendBase
{
    public const string BackendName = "OpenAL";

    // Runtime registry, so FMOD-only builds don't include OpenAL code
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register() => VAAudioBackendRegistry.Register(BackendName, () => new OpenALBackend());

    ALDevice ALDevice;
    ALContext ALContext;

    public override string Name => BackendName;

    public override bool IsInitialised => ALDevice != null;

    public override void Initialise()
    {
        if (IsInitialised)
            return;

        // Log to both - in case we're launched from vs2026 or from the Godot Editor
        OpenAL.Logger.Log = (message) =>
        {
            Console.WriteLine(message);
            GD.Print(message);
        };
        OpenAL.Logger.Error = LogError;

        CreateDeviceAndContext();
    }

    public override void Update()
    {
        if (!IsInitialised || !CheckDeviceConnected())
            return;

        ALContext.Process();
    }

    public override void Shutdown()
    {
        if (IsInitialised)
            DestroyDevice();
    }

    protected override void ApplyMasterVolume(float volume) => AL.Listenerf(AL.AL_GAIN, volume);
    protected override void ApplyDistanceModel(VADistanceModel model) => AL.DistanceModel((int)ToAL(model));
    protected override void ApplyMetersPerUnit(float metersPerUnit) => AL.Listenerf(AL.AL_METERS_PER_UNIT, metersPerUnit);
    protected override void ApplySpeedOfSound(float speedOfSound) => AL.SpeedOfSound(speedOfSound);
    protected override void ApplyDopplerScale(float scale) => AL.DopplerFactor(scale);
    protected override void ApplyListenerPosition(Vector3 position) => AL.Listenerfv(AL.AL_POSITION, [position.X, position.Y, position.Z]);
    protected override void ApplyListenerVelocity(Vector3 velocity) => AL.Listenerfv(AL.AL_VELOCITY, [velocity.X, velocity.Y, velocity.Z]);
    protected override void ApplyListenerOrientation(Vector3 forward, Vector3 up) => AL.Listenerfv(AL.AL_ORIENTATION, [forward.X, forward.Y, forward.Z, up.X, up.Y, up.Z]);

    static ALDistanceModel ToAL(VADistanceModel model) => model switch
    {
        VADistanceModel.None => ALDistanceModel.None,
        VADistanceModel.Inverse => ALDistanceModel.InverseDistance,
        VADistanceModel.Linear => ALDistanceModel.LinearDistance,
        VADistanceModel.LinearClamped => ALDistanceModel.LinearDistanceClamped,
        VADistanceModel.Exponent => ALDistanceModel.ExponentDistance,
        VADistanceModel.ExponentClamped => ALDistanceModel.ExponentDistanceClamped,
        _ => ALDistanceModel.InverseDistanceClamped,
    };

    internal static int ToAL(VAPcmFormat format) => format switch
    {
        VAPcmFormat.Mono8 => AL.AL_FORMAT_MONO8,
        VAPcmFormat.Mono16 => AL.AL_FORMAT_MONO16,
        VAPcmFormat.Stereo8 => AL.AL_FORMAT_STEREO8,
        VAPcmFormat.Stereo16 => AL.AL_FORMAT_STEREO16,
        VAPcmFormat.MonoFloat => AL.AL_FORMAT_MONO_FLOAT32,
        _ => AL.AL_FORMAT_STEREO_FLOAT32,
    };

    public override unsafe VASound CreateSound(short[] pcm16, int channels, int sampleRate)
    {
        // The device can be destroyed while a sound decodes on a worker thread
        var context = ALContext;

        if (context == null)
            return null;

        context.MakeCurrent();

        var handle = AL.GenBuffer();

        fixed (short* data = pcm16)
            AL.BufferData(handle, AL.GetSoundFormat(channels, 16), (nint)data, pcm16.Length * sizeof(short), sampleRate);

        return new OpenALSound(this, handle);
    }

    // Wraps a new AL source, or logs and returns null when OpenAL is out of sources
    internal uint GenSource()
    {
        var id = AL.GenSource();

        if (id == 0)
            LogError("Failed to create a source - likely too many sources have been created. Increase audio/vaudio/openal_max_mono_sources or openal_max_stereo_sources");

        return id;
    }

    public override bool TryCreateStream(VAPcmFormat format, int sampleRate, out VAStream stream)
    {
        var id = GenSource();
        stream = id == 0 ? null : new OpenALStream(new ALStreamSource(id, ToAL(format), sampleRate));
        return stream != null;
    }

    public override VAReverbEffect CreateReverbEffect() => new OpenALReverbEffect();

    public override string[] GetCaptureDevices() => [.. AL.GetStringList(IntPtr.Zero, AL.ALC_CAPTURE_DEVICE_SPECIFIER)];

    public override VACaptureDevice OpenCaptureDevice(string deviceName, VAPcmFormat format, int sampleRate, int bufferFrames, Action<nint, int> onData)
    {
        return new OpenALCaptureDevice(new ALCaptureDevice(new()
        {
            DeviceName = string.IsNullOrEmpty(deviceName) ? null : deviceName,
            Format = ToAL(format),
            SampleRate = sampleRate,
            BufferSize = bufferFrames,
            LogCallback = LogWarning,
            DataCallback = onData,
        }));
    }
}
