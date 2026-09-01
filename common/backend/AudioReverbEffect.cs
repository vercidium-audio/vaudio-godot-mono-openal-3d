namespace vaudio_godot_mono_openal;

/// <summary>
/// Backend-agnostic reverb parameters. This is the superset of every knob ALReverbEffect exposed (EFX EAXREVERB); each backend consumes what it can. FMOD's FMOD_REVERB_PROPERTIES has no separate LF band, echo or modulation, and no reflections/late-reverb pan - those fields are simply ignored by the FMOD backend.
///
/// Pure data - no device handles, no GenEffect. The backend's <see cref="IAudioReverbSlot"/> owns the real effect and reads these fields in <see cref="IAudioReverbSlot.Push"/>.
/// </summary>
public class AudioReverbEffect
{
    /// <summary>Set by writers when any field changed; the backend clears it after applying.</summary>
    public bool dirty = true;

    /// <summary>Modal density of the reverb tail.</summary>
    public float density;

    /// <summary>Diffusion of sound energy throughout the reverb field.</summary>
    public float diffusion;

    /// <summary>Master output level for the reverb effect.</summary>
    public float gain;

    /// <summary>High-frequency damping factor.</summary>
    public float gainHF;

    /// <summary>Low-frequency gain adjustment. (No FMOD target.)</summary>
    public float gainLF;

    /// <summary>Reverb decay time in seconds.</summary>
    public float decayTime;

    /// <summary>High-frequency decay ratio.</summary>
    public float decayHFRatio;

    /// <summary>Low-frequency decay ratio. (No FMOD target.)</summary>
    public float decayLFRatio;

    /// <summary>Early reflections gain.</summary>
    public float reflectionsGain;

    /// <summary>Early reflections delay.</summary>
    public float reflectionsDelay;

    /// <summary>Early reflections panning vector. (No FMOD target.)</summary>
    public float[] reflectionsPan = [0, 0, 0];

    /// <summary>Late reverb gain.</summary>
    public float lateReverbGain;

    /// <summary>Late reverb delay.</summary>
    public float lateReverbDelay;

    /// <summary>Late reverb panning vector. (No FMOD target.)</summary>
    public float[] lateReverbPan = [0, 0, 0];

    /// <summary>Echo time. (No FMOD target.)</summary>
    public float echoTime;

    /// <summary>Echo depth. (No FMOD target.)</summary>
    public float echoDepth;

    /// <summary>Modulation time. (No FMOD target.)</summary>
    public float modulationTime;

    /// <summary>Modulation depth. (No FMOD target.)</summary>
    public float modulationDepth;

    /// <summary>Air absorption gain for high frequencies.</summary>
    public float airAbsorptionGainHF;

    /// <summary>High-frequency reference.</summary>
    public float hfReference;

    /// <summary>Low-frequency reference.</summary>
    public float lfReference;

    /// <summary>Room rolloff factor.</summary>
    public float roomRolloffFactor;

    /// <summary>High-frequency decay limit flag (0/1).</summary>
    public int decayHFLimit;

    /// <summary>Gain of the entire effect slot.</summary>
    public float effectSlotGain = 1;
}
