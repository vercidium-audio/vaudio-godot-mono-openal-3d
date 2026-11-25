// AI-created

namespace vaudio_godot_mono_openal;

// TEMP: placeholder until FMOD reverb is implemented. Invalid, so VAWorld treats the listener's reverb as ready and sources play dry
sealed class FMODReverbEffect : VAReverbEffect
{
    public override bool IsValid => false;

    public override void SetParams(vaudio.EAXReverb eax, float gain, Vector3 reflectionsPan, Vector3 lateReverbPan) { }

    public override void Dispose() { }
}
