// AI-created

namespace vaudio_godot_mono_openal;

// A decoded sound held in FMOD's memory
sealed class FMODSound(FMODBackend backend, FMOD.Sound sound, int sampleRate, float length) : VASound
{
    FMOD.Sound sound = sound;

    public override float Length => length;

    public override bool TryCreateVoice(bool spatialised, out VAVoice voice)
    {
        voice = sound.hasHandle() ? backend.PlaySound(sound, sampleRate, length, spatialised) : null;
        return voice != null;
    }

    public override void Dispose()
    {
        // System::release already freed it with the device
        if (sound.hasHandle() && backend.IsInitialised)
            backend.Check(sound.release(), "Sound::release");

        sound.clearHandle();
    }
}
