// AI-created

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace vaudio_godot_mono_openal;

// Audio backend powered by FMOD Core
public sealed partial class FMODBackend : VAAudioBackendBase
{
    public const string BackendName = "FMOD";

    // Runtime registry, so OpenAL-only builds don't include FMOD code
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register() => VAAudioBackendRegistry.Register(BackendName, () => new FMODBackend());

    FMOD.System _system;
    FMOD.ChannelGroup _masterGroup;

    // Initialise() logs a failure once, rather than again for every VAWorld that enters the tree
    bool _initialiseFailed;

    public override string Name => BackendName;

    public override bool IsInitialised => _system.hasHandle();

    public override void Initialise()
    {
        if (IsInitialised || _initialiseFailed)
            return;

        EnsureSettingsRead();

        if (!LoadLibrary() || !CreateSystem())
            _initialiseFailed = true;
    }

    public override void Update()
    {
        if (IsInitialised)
            Check(_system.update(), "System::update");
    }

    public override void Shutdown()
    {
        if (!IsInitialised)
            return;

        RaiseDeviceDestroying();

        // Anything still undisposed is freed by System::release
        foreach (var voice in new List<FMODVoice>(_voices))
            voice.Dispose();

        Check(_system.release(), "System::release");
        _system.clearHandle();
        _masterGroup.clearHandle();
    }

    bool _libraryLoaded;

    bool LoadLibrary()
    {
        if (_libraryLoaded)
            return true;

        string loadedFrom = FMODLibrary.Load(_libraryPath, out string error);

        if (loadedFrom == null)
        {
            LogError(error);
            return false;
        }

        Log($"Loaded FMOD from {loadedFrom}");
        _libraryLoaded = true;
        return true;
    }

    // Creates and initialises the FMOD system with the project settings. Logs and returns false on failure
    bool CreateSystem()
    {
        // Shouldn't be initialising in the editor
        Debug.Assert(!Engine.IsEditorHint());

        var result = FMOD.Factory.System_Create(out var system);

        if (result == FMOD.RESULT.ERR_HEADER_MISMATCH)
        {
            LogError($"The FMOD library's version doesn't match the FMOD {FormatVersion(FMOD.VERSION.number)} API vaudio was built with. Use an FMOD Engine 2.03.x library");
            return false;
        }

        if (!Check(result, "System_Create"))
            return false;

        if (_sampleRate > 0)
            Check(system.setSoftwareFormat(_sampleRate, FMOD.SPEAKERMODE.DEFAULT, 0), "System::setSoftwareFormat");

        // FMOD's default is 4 buffers of 1024 samples
        if (_dspBufferSize > 0)
            Check(system.setDSPBufferSize((uint)_dspBufferSize, 4), "System::setDSPBufferSize");

        SelectDriver(system);

        // Godot is right-handed, FMOD defaults to left-handed
        if (!Check(system.init(_maxChannels, FMOD.INITFLAGS._3D_RIGHTHANDED, IntPtr.Zero), "System::init") || !Check(system.getMasterChannelGroup(out _masterGroup), "System::getMasterChannelGroup"))
        {
            system.release();
            return false;
        }

        _system = system;

        Check(_system.getVersion(out uint version), "System::getVersion");
        Log($"FMOD {FormatVersion(version)} initialised");

        ApplyAllSettings();
        return true;
    }

    static string FormatVersion(uint version) => $"{version >> 16:x}.{(version >> 8) & 0xff:x2}.{version & 0xff:x2}";
    static string FormatVersion(int version) => FormatVersion((uint)version);

    // Logs a failed FMOD call and counts it as a backend error. Returns whether it succeeded
    internal bool Check(FMOD.RESULT result, string call)
    {
        if (result == FMOD.RESULT.OK)
            return true;

        LogError($"FMOD {call} failed: {FMOD.Error.String(result)} ({result})");
        return false;
    }

    protected override void ApplyMasterVolume(float volume) => Check(_masterGroup.setVolume(volume), "ChannelGroup::setVolume");

    // FMOD has no global distance model, so each voice samples it into its own rolloff curve
    protected override void ApplyDistanceModel(VADistanceModel model)
    {
        foreach (var voice in _voices)
            voice.ApplyRolloff();
    }

    protected override void ApplyMetersPerUnit(float metersPerUnit) => Apply3DSettings();
    protected override void ApplySpeedOfSound(float speedOfSound) => Apply3DSettings();
    protected override void ApplyDopplerScale(float scale) => Apply3DSettings();

    // FMOD's doppler uses a fixed speed of sound, in metres per second
    const float FMODSpeedOfSound = 340;

    // FMOD has no speed of sound setting, so it's folded into the doppler scale: scaling every velocity by FMODSpeedOfSound / SpeedOfSound gives the same pitch shift as changing the speed of sound
    void Apply3DSettings()
    {
        float dopplerScale = DopplerScale * FMODSpeedOfSound / MathF.Max(0.0001f, SpeedOfSound);
        float unitsPerMetre = 1 / MathF.Max(0.0001f, MetersPerUnit);

        Check(_system.set3DSettings(dopplerScale, unitsPerMetre, 1), "System::set3DSettings");
    }

    FMOD.VECTOR _listenerPosition;
    FMOD.VECTOR _listenerVelocity;
    // FMOD rejects every listener update while these aren't unit length and perpendicular, and ApplyAllSettings sets the position first
    FMOD.VECTOR _listenerForward = new() { z = -1 };
    FMOD.VECTOR _listenerUp = new() { y = 1 };

    protected override void ApplyListenerPosition(Vector3 position)
    {
        _listenerPosition = ToFMOD(position);
        ApplyListener();
    }

    protected override void ApplyListenerVelocity(Vector3 velocity)
    {
        _listenerVelocity = ToFMOD(velocity);
        ApplyListener();
    }

    protected override void ApplyListenerOrientation(Vector3 forward, Vector3 up)
    {
        _listenerForward = ToFMOD(forward);
        _listenerUp = ToFMOD(up);
        ApplyListener();
    }

    void ApplyListener() => Check(_system.set3DListenerAttributes(0, ref _listenerPosition, ref _listenerVelocity, ref _listenerForward, ref _listenerUp), "System::set3DListenerAttributes");

    internal static FMOD.VECTOR ToFMOD(Vector3 v) => new() { x = v.X, y = v.Y, z = v.Z };

    internal FMOD.System System => _system;

    // Every undisposed voice, so a distance model change reaches them all
    readonly HashSet<FMODVoice> _voices = [];

    internal void AddVoice(FMODVoice voice) => _voices.Add(voice);
    internal void RemoveVoice(FMODVoice voice) => _voices.Remove(voice);

    public override unsafe VASound CreateSound(short[] pcm16, int channels, int sampleRate)
    {
        // The device can be destroyed while a sound decodes on a worker thread
        var system = _system;

        if (!system.hasHandle())
            return null;

        var exinfo = new FMOD.CREATESOUNDEXINFO
        {
            cbsize = Marshal.SizeOf<FMOD.CREATESOUNDEXINFO>(),
            length = (uint)(pcm16.Length * sizeof(short)),
            numchannels = channels,
            defaultfrequency = sampleRate,
            format = FMOD.SOUND_FORMAT.PCM16,
        };

        // FMOD copies the data. Each voice sets its own 2D/3D, relative and loop mode
        var mode = FMOD.MODE.OPENMEMORY | FMOD.MODE.OPENRAW | FMOD.MODE.CREATESAMPLE | FMOD.MODE.LOOP_OFF | FMOD.MODE._3D;
        FMOD.Sound sound;

        fixed (short* data = pcm16)
        {
            if (!Check(system.createSound((IntPtr)data, mode, ref exinfo, out sound), "System::createSound"))
                return null;
        }

        return new FMODSound(this, sound, sampleRate, pcm16.Length / channels / (float)sampleRate);
    }

    // Plays the sound paused on a new channel. Returns null if FMOD failed, which it logs
    internal FMODVoice PlaySound(FMOD.Sound sound, int sampleRate, float length, bool spatialised, FMOD.Sound streamSound = default)
    {
        if (!IsInitialised || !Check(_system.playSound(sound, default, true, out var channel), "System::playSound"))
            return null;

        return new FMODVoice(this, channel, sampleRate, length, spatialised, streamSound);
    }

    public override bool TryCreateStream(VAPcmFormat format, int sampleRate, out VAStream stream)
    {
        stream = IsInitialised ? FMODStream.Create(this, format, sampleRate) : null;
        return stream != null;
    }

    // TEMP: reverb isn't implemented yet. An invalid effect counts as ready, so sources play without reverb
    public override VAReverbEffect CreateReverbEffect() => new FMODReverbEffect();

    // TEMP: capture isn't implemented yet
    public override string[] GetCaptureDevices() => [];

    public override VACaptureDevice OpenCaptureDevice(string deviceName, VAPcmFormat format, int sampleRate, int bufferFrames, Action<nint, int> onData) => throw new NotSupportedException("FMOD capture isn't implemented yet");
}
