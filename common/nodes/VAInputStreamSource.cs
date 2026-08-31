using System.Runtime.InteropServices;

using Godot.Collections;

namespace vaudio_godot_mono_openal;

// A VAStreamSource fed automatically by a local input (microphone) capture device. Owns its own ALCaptureDevice, so multiple nodes can each capture from a different device/format at once. See VANetworkedStreamSource for the network equivalent.
[Tool]
[GlobalClass]
public partial class VAInputStreamSource : VAStreamSource
{
    // Shown in the DeviceName dropdown in place of "" (the driver's default), since a strict enum's current value must be one of its own entries. Matches the native plugin's DEFAULT_DEVICE_LABEL.
    public const string DefaultDeviceLabel = "System Default";

    [Signal]
    public delegate void AudioCapturedEventHandler(byte[] data);

    ALCaptureDevice captureDevice;

    public bool IsCaptureOpen => captureDevice != null;

    int _format = AL.AL_FORMAT_MONO16;
    int _sampleRate = 44100;
    string _deviceName = "";
    int _bufferSizeFrames = 1024;
    int _microphoneThreshold = 0;

    [Export(PropertyHint.Enum, "Mono 8-bit:4352,Mono 16-bit:4353,Stereo 8-bit:4354,Stereo 16-bit:4355")]
    public int Format
    {
        get => _format;
        set => _format = value;
    }

    [Export(PropertyHint.Enum, "22050,44100,48000,96000")]
    public int SampleRate
    {
        get => _sampleRate;
        set => _sampleRate = value;
    }

    // _deviceName stores "" for the driver's default (what OpenCapture expects); the inspector property maps that to/from DefaultDeviceLabel so the enum dropdown doesn't show a blank entry.
    /// <summary>Empty for the driver's default input device, or a name from GetAvailableCaptureDevices.</summary>
    [Export]
    public string DeviceName
    {
        get => string.IsNullOrEmpty(_deviceName) ? DefaultDeviceLabel : _deviceName;
        set => _deviceName = value == DefaultDeviceLabel ? "" : value;
    }

    [Export(PropertyHint.Range, "1,65536,1,or_greater")]
    public int BufferSizeFrames
    {
        get => _bufferSizeFrames;
        set => _bufferSizeFrames = Math.Max(1, value);
    }

    /// <summary>Minimum peak amplitude (0-32767, 16-bit scale regardless of Format) a captured chunk must reach before it's forwarded. 0 disables the gate.</summary>
    [Export(PropertyHint.Range, "0,32767,or_greater")]
    public int MicrophoneThreshold
    {
        get => _microphoneThreshold;
        set => _microphoneThreshold = Math.Max(0, value);
    }

    public static string[] GetAvailableCaptureDevices() => [.. AL.GetStringList(IntPtr.Zero, AL.ALC_CAPTURE_DEVICE_SPECIFIER)];

    public override void _Ready()
    {
        base._Ready();

        if (Engine.IsEditorHint())
            return;

        if (!OpenStream(Format, SampleRate))
            return;

        if (!OpenCapture(_deviceName, Format, SampleRate, BufferSizeFrames))
            return;

        StartCapture();
    }

    // 8-bit samples are unsigned (silence = 128) and scaled up by 256 so MicrophoneThreshold means
    // the same thing regardless of Format; 16-bit samples are signed (silence = 0).
    static unsafe int PeakAmplitude(byte* data, int numBytes, int format)
    {
        int peak = 0;

        if (format == AL.AL_FORMAT_MONO8 || format == AL.AL_FORMAT_STEREO8)
        {
            for (int i = 0; i < numBytes; i++)
            {
                var amplitude = Math.Abs(data[i] - 128) * 256;
                peak = amplitude > peak ? amplitude : peak;
            }
        }
        else
        {
            var samples = (short*)data;
            var sampleCount = numBytes / 2;

            for (int i = 0; i < sampleCount; i++)
            {
                var amplitude = Math.Abs((int)samples[i]);
                peak = amplitude > peak ? amplitude : peak;
            }
        }

        return peak;
    }

    public unsafe bool OpenCapture(string deviceName, int format, int frequency, int bufferSizeFrames)
    {
        CloseCapture();

        try
        {
            captureDevice = new ALCaptureDevice(new()
            {
                DeviceName = string.IsNullOrEmpty(deviceName) ? null : deviceName,
                Format = format,
                SampleRate = frequency,
                BufferSize = bufferSizeFrames,
                LogCallback = LogWarning,
                DataCallback = (samples, sampleCount) =>
                {
                    var numBytes = sampleCount * AL.GetBytesPerFrame(format);
                    var data = (byte*)samples;

                    if (PeakAmplitude(data, numBytes, format) < MicrophoneThreshold)
                        return;

                    var packed = new byte[numBytes];
                    Marshal.Copy((nint)data, packed, 0, numBytes);

                    if (IsStreamOpen)
                        PushAudioData(packed);

                    EmitSignal(SignalName.AudioCaptured, packed);
                },
            });
        }
        catch (Exception e)
        {
            LogWarning($"Failed to open capture device '{deviceName}' on {Name}: {e.Message}");
            return false;
        }

        return true;
    }

    public void StartCapture()
    {
        if (captureDevice == null)
        {
            LogWarning($"StartCapture called on {Name} before OpenCapture (or after CloseCapture)");
            return;
        }

        captureDevice.CaptureStart();
    }

    public void StopCapture()
    {
        captureDevice?.CaptureStop();
    }

    public void CloseCapture()
    {
        if (captureDevice == null)
            return;

        captureDevice.Close();
        captureDevice = null;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        captureDevice?.Update();
    }

    public override void _ExitTree()
    {
        CloseCapture();

        base._ExitTree();
    }

    // Rebuilds DeviceName as a strict PropertyHint.Enum dropdown of the driver's current capture devices - EnumSuggestion would inject an unsuppressable blank entry. An unrecognized saved value shows as-is until RefreshDevices() re-queries.
    public override void _ValidateProperty(Dictionary property)
    {
        base._ValidateProperty(property);

        if (property["name"].AsStringName() != PropertyName.DeviceName)
            return;

        var devices = GetAvailableCaptureDevices();

        if (devices.Length == 0)
            return;

        var hintString = DefaultDeviceLabel + "," + string.Join(",", devices);

        property["hint"] = (int)PropertyHint.Enum;
        property["hint_string"] = hintString;
    }

    // Re-queries the driver's capture device list so a newly plugged-in device shows up without reselecting the node - VADeviceRefreshInspectorPlugin.gd calls this.
    public void RefreshDevices()
    {
        NotifyPropertyListChanged();
    }
}
