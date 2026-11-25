// AI-created

using System.Runtime.InteropServices;

namespace vaudio_godot_mono_openal;

// Records a capture driver into a looping user-created sound, and Poll() hands what's been recorded since the last poll to onData
sealed unsafe class FMODCaptureDevice : VACaptureDevice
{
    // The recording loops round this much audio, so it only has to be polled more often than this
    const float RecordBufferSeconds = 1;

    readonly FMODBackend backend;
    readonly int driver;
    readonly string deviceName;
    readonly VAPcmFormat format;
    readonly Action<nint, int> onData;
    readonly uint lengthFrames;

    FMOD.Sound sound;
    uint readFrame;
    bool recording;
    byte[] scratch = [];

    FMODCaptureDevice(FMODBackend backend, int driver, string deviceName, VAPcmFormat format, Action<nint, int> onData, FMOD.Sound sound, uint lengthFrames)
    {
        this.backend = backend;
        this.driver = driver;
        this.deviceName = deviceName;
        this.format = format;
        this.onData = onData;
        this.sound = sound;
        this.lengthFrames = lengthFrames;

        // System::release frees the sound and stops recording
        backend.DeviceDestroying += OnDeviceDestroying;
    }

    // Throws if the device can't be opened, like OpenAL's
    internal static FMODCaptureDevice Open(FMODBackend backend, string deviceName, VAPcmFormat format, int sampleRate, int bufferFrames, Action<nint, int> onData)
    {
        if (!backend.IsInitialised)
            throw new InvalidOperationException("the FMOD backend isn't initialised");

        int driver = backend.FindRecordDriver(deviceName);

        if (driver < 0)
            throw new InvalidOperationException(string.IsNullOrEmpty(deviceName) ? "FMOD has no default capture device" : $"FMOD has no capture device named '{deviceName}'");

        // FMOD resamples and remixes to the sound's rate and channels. At least the requested buffer, like OpenAL's capture ring
        uint lengthFrames = (uint)Math.Max(bufferFrames, sampleRate * RecordBufferSeconds);

        var exinfo = new FMOD.CREATESOUNDEXINFO
        {
            cbsize = Marshal.SizeOf<FMOD.CREATESOUNDEXINFO>(),
            numchannels = format.Channels(),
            defaultfrequency = sampleRate,
            format = FMODBackend.ToFMOD(format),
            length = lengthFrames * (uint)format.BytesPerFrame(),
        };

        if (!backend.Check(backend.System.createSound(IntPtr.Zero, FMOD.MODE.OPENUSER | FMOD.MODE.LOOP_NORMAL | FMOD.MODE.CREATESAMPLE | FMOD.MODE._2D, ref exinfo, out var sound), "System::createSound"))
            throw new InvalidOperationException("FMOD couldn't create the recording buffer");

        return new FMODCaptureDevice(backend, driver, deviceName, format, onData, sound, lengthFrames);
    }

    public override void Start()
    {
        if (recording || !sound.hasHandle())
            return;

        // Recording restarts from the start of the sound
        readFrame = 0;
        recording = backend.Check(backend.System.recordStart(driver, sound, true), "System::recordStart");
    }

    public override void Stop()
    {
        if (!recording)
            return;

        // Hand over what was recorded before the stop, like OpenAL's capture buffer
        Poll();
        recording = false;
        backend.Check(backend.System.recordStop(driver), "System::recordStop");
    }

    public override void Poll()
    {
        if (!recording)
            return;

        var result = backend.System.getRecordPosition(driver, out uint recordFrame);

        // Unplugging isn't a backend error, it just stops the recording
        if (result == FMOD.RESULT.ERR_RECORD_DISCONNECTED)
        {
            Logger.LogWarning($"The capture device '{deviceName}' was disconnected, so capturing stopped");
            recording = false;
            return;
        }

        if (!backend.Check(result, "System::getRecordPosition") || recordFrame == readFrame)
            return;

        uint frames = (recordFrame + lengthFrames - readFrame) % lengthFrames;
        int bytesPerFrame = format.BytesPerFrame();
        int numBytes = (int)frames * bytesPerFrame;

        if (!backend.Check(sound.@lock(readFrame * (uint)bytesPerFrame, (uint)numBytes, out var ptr1, out var ptr2, out uint len1, out uint len2), "Sound::lock"))
            return;

        if (scratch.Length < numBytes)
            scratch = new byte[numBytes];

        // The two parts are where the recording wrapped round the end of the sound
        Marshal.Copy(ptr1, scratch, 0, (int)len1);

        if (ptr2 != IntPtr.Zero && len2 > 0)
            Marshal.Copy(ptr2, scratch, (int)len1, (int)len2);

        backend.Check(sound.unlock(ptr1, ptr2, len1, len2), "Sound::unlock");
        readFrame = recordFrame;

        fixed (byte* data = scratch)
        {
            if (format.BytesPerSample() == 1)
                FMODBackend.ToggleSign8(data, (uint)numBytes);

            onData((nint)data, (int)frames);
        }
    }

    public override void Close()
    {
        backend.DeviceDestroying -= OnDeviceDestroying;

        if (!sound.hasHandle())
            return;

        Stop();
        backend.Check(sound.release(), "Sound::release");
        sound.clearHandle();
    }

    void OnDeviceDestroying()
    {
        recording = false;
        sound.clearHandle();
    }
}
