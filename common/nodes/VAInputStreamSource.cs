// Human-created

using System.Runtime.InteropServices;

using Godot.Collections;

namespace vaudio_godot_mono_openal;

// A VAStreamSource that plays back a local capture device
[Tool]
[GlobalClass]
public partial class VAInputStreamSource : VAStreamSource
{
    public const string DefaultDeviceLabel = "System Default";

    [Signal]
    public delegate void AudioCapturedEventHandler(byte[] data);

    VACaptureDevice captureDevice;

    public bool IsCaptureOpen => captureDevice != null;

    VAPcmFormat _format = VAPcmFormat.Mono16;
    int _sampleRate = 44100;
    string _deviceName = "";
    int _bufferSizeFrames = 1024;
    int _microphoneThreshold = 0;

    /// <summary>The captured audio's channels and sample type. Only the 8 and 16-bit formats are supported</summary>
    [Export(PropertyHint.Enum, "Mono 8-bit:0,Mono 16-bit:1,Stereo 8-bit:2,Stereo 16-bit:3")]
    public VAPcmFormat Format
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

    public bool MicrophoneThresholdEnabled => MicrophoneThreshold > 0;

    public static string[] GetAvailableCaptureDevices() => VAAudioBackend.GetCaptureDevices();

    // Enumerating capture devices can be slow, and Godot calls _ValidateProperty every time it fetches the property list, so the dropdown reuses this array until RefreshDevices() is called
    static string[] cachedCaptureDevices;

    // PeakAmplitude only reads 8 and 16-bit PCM, matching the Format dropdown
    static bool IsSupportedFormat(VAPcmFormat format) => format.BytesPerSample() <= 2;

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

    static unsafe int PeakAmplitude(byte* data, int numBytes, VAPcmFormat format)
    {
        int peak = 0;

        if (format.BytesPerSample() == 1)
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

    public unsafe bool OpenCapture(string deviceName, VAPcmFormat format, int frequency, int bufferSizeFrames)
    {
        CloseCapture();

        if (!IsSupportedFormat(format))
        {
            LogWarning($"{Name}: {format} isn't a supported capture format. Use Mono8, Mono16, Stereo8 or Stereo16 instead");
            return false;
        }

        try
        {
            captureDevice = VAAudioBackend.OpenCaptureDevice(deviceName, format, frequency, bufferSizeFrames, (samples, sampleCount) =>
            {
                var numBytes = sampleCount * format.BytesPerFrame();
                var data = (byte*)samples;

                // Ignore quiet input
                if (MicrophoneThresholdEnabled)
                    if (PeakAmplitude(data, numBytes, format) < MicrophoneThreshold)
                        return;

                var packed = new byte[numBytes];
                Marshal.Copy((nint)data, packed, 0, numBytes);

                if (IsStreamOpen)
                    PushAudioData(packed);

                EmitSignal(SignalName.AudioCaptured, packed);
            });
        }
        catch (Exception e)
        {
            VAAudioBackend.LogBackendError($"Failed to open capture device '{deviceName}' on {Name}: {e.Message}");
            return false;
        }

        return true;
    }

    public void StartCapture()
    {
        // Reachable from scripts, e.g. after a failed OpenCapture or after CloseCapture
        if (captureDevice == null)
        {
            LogWarning($"StartCapture called on {Name} before OpenCapture (or after CloseCapture)");
            return;
        }

        captureDevice.Start();
    }

    public void StopCapture()
    {
        captureDevice?.Stop();
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

        captureDevice?.Poll();
    }

    public override void _ExitTree()
    {
        CloseCapture();

        base._ExitTree();
    }

    // Rebuilds DeviceName as a strict PropertyHint.Enum dropdown, for nicer display to the user.
    // Using EnumSuggestion doesn't work, as the default device label is an empty string.
    // An unrecognized saved value shows as-is until RefreshDevices() re-queries.
    public override void _ValidateProperty(Dictionary property)
    {
        base._ValidateProperty(property);

        if (property["name"].AsStringName() == PropertyName.DeviceName)
        {
            cachedCaptureDevices ??= GetAvailableCaptureDevices();

            // Still a dropdown with no devices, so it only offers the default rather than becoming a free text field
            property["hint"] = (int)PropertyHint.Enum;
            property["hint_string"] = string.Join(",", [DefaultDeviceLabel, .. cachedCaptureDevices]);
        }
    }

    // Called by VADeviceRefreshInspectorPlugin.gd to refresh the input device list
    public void RefreshDevices()
    {
        cachedCaptureDevices = null;
        NotifyPropertyListChanged();
    }
}
