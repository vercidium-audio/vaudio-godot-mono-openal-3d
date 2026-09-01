namespace vaudio_godot_mono_openal;

/// <summary>
/// Backend-agnostic distance attenuation model. Mirrors OpenAL's ALDistanceModel; the FMOD backend maps these onto its own rolloff modes.
/// </summary>
public enum AudioDistanceModel
{
    None,
    InverseDistance,
    InverseDistanceClamped,
    LinearDistance,
    LinearDistanceClamped,
    ExponentDistance,
    ExponentDistanceClamped,
}
