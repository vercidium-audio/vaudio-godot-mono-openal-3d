using System.Runtime.InteropServices;

using Godot.Collections;

namespace vaudio_godot_mono_openal_3d;

// A VAStreamSource fed automatically by a local input (microphone) capture device. Owns its own ALCaptureDevice, so multiple nodes can each capture from a different device/format at once. SeeVANetworkedStreamSource for the network equivalent.
[Tool]
[GlobalClass]
public partial class VAInputStreamSource : VAStreamSource
{
    // Matches the native plugin's DEFAULT_DEVICE_LABEL (va_device_name.h) - shown in the
    // DeviceName dropdown in place of "" (the driver's default capture device), since a strict
    // PROPERTY_HINT_ENUM's current value must be one of its own entries.
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

    // _deviceName itself stores "" for "use the driver's default capture device" (what
    // OpenCapture expects), but the property exposed to the inspector translates that to/from
    // DefaultDeviceLabel, since the Inspector's enum dropdown (_ValidateProperty) shows the
    // property's current value as one of its own entries and an unlabelled "" would otherwise
    // show as a blank entry. Matches the native plugin's get_device_name/set_device_name.
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
                LogCallback = GD.PushWarning,
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

    // Turns DeviceName into a strict enum dropdown of the driver's currently available capture
    // devices - Godot's editor unconditionally injects a blank entry into a string
    // PropertyHint.EnumSuggestion dropdown, with no way to suppress it, so this uses
    // PropertyHint.Enum instead. Safe despite DeviceName being runtime-dependent: an unrecognized
    // saved value is shown as-is, just not pre-selected until RefreshDevices() re-queries the
    // driver. Matches the native plugin's VAInputStreamSource::_validate_property.
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

    // Re-queries the driver's capture device list and re-validates DeviceName's dropdown so a
    // device plugged in after this node was selected shows up without reselecting the node - see
    // VADeviceRefreshInspectorPlugin.gd for the Inspector button that calls this. Matches the
    // native plugin's VAInputStreamSource::refresh_devices.
    public void RefreshDevices()
    {
        NotifyPropertyListChanged();
    }
}
