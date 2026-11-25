namespace vaudio_godot_mono_openal;

// An EFX EAX reverb effect in an auxiliary effect slot
sealed class OpenALReverbBus : VAReverbBus
{
    internal ALReverbEffect Effect { get; private set; } = new();

    // Silent until SetParams writes its slot gain with the first EAX, so nothing plays through OpenAL's default reverb
    public OpenALReverbBus() => AL.AuxiliaryEffectSlotf(Effect.effectSlotID, AL.AL_EFFECTSLOT_GAIN, 0);

    public override bool IsValid => Effect != null && Effect.effectSlotID != 0;

    public override void SetParams(vaudio.EAXReverb eax, float gain, Vector3 reflectionsPan, Vector3 lateReverbPan)
    {
        var effect = Effect;

        effect.gain = 1f;

        // Density causes static when updating in real time
        //  See OpenAL Soft GitHub issue: https://github.com/kcat/openal-soft/issues/1229
        effect.density = 0.5f;//eax.Density;

        effect.diffusion = eax.Diffusion;
        effect.gainLF = eax.GainLF;
        effect.gainHF = eax.GainHF;
        effect.decayTime = eax.DecayTime;
        effect.decayLFRatio = eax.DecayLFRatio;
        effect.decayHFRatio = eax.DecayHFRatio;
        effect.reflectionsDelay = eax.ReflectionsDelay;
        effect.reflectionsGain = eax.ReflectionsGain;
        effect.lateReverbGain = eax.LateReverbGain;
        effect.lateReverbDelay = eax.LateReverbDelay;
        effect.echoTime = eax.EchoTime;
        effect.echoDepth = eax.EchoDepth;
        effect.modulationTime = eax.ModulationTime;
        effect.modulationDepth = eax.ModulationDepth;
        effect.airAbsorptionGainHF = eax.AirAbsorptionGainHF;
        effect.hfReference = eax.HFReference;
        effect.lfReference = eax.LFReference;
        effect.roomRolloffFactor = eax.RoomRolloffFactor;
        effect.decayHFLimit = eax.DecayHFLimit;

        effect.effectSlotGain = gain;

        effect.reflectionsPan[0] = reflectionsPan.X;
        effect.reflectionsPan[1] = reflectionsPan.Y;
        effect.reflectionsPan[2] = reflectionsPan.Z;

        effect.lateReverbPan[0] = lateReverbPan.X;
        effect.lateReverbPan[1] = lateReverbPan.Y;
        effect.lateReverbPan[2] = lateReverbPan.Z;

        effect.dirty = true;
        effect.Update();
    }

    public override void Dispose()
    {
        Effect?.Dispose();
        Effect = null;
    }
}
