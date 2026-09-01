namespace vaudio_godot_mono_openal;

/// <summary>
/// Wraps an <see cref="ALReverbEffect"/> (EFX EAXREVERB effect + aux effect slot) as an <see cref="IAudioReverbSlot"/>. <see cref="Push"/> copies the backend-agnostic <see cref="AudioReverbEffect"/> DTO onto the internal effect and applies it.
/// </summary>
public class OpenALReverbSlot : IAudioReverbSlot
{
    readonly ALReverbEffect effect = new();

    /// <summary>The underlying EFX effect, consumed by <see cref="OpenALSourceHandle.ApplyReverb"/>.</summary>
    internal ALReverbEffect Effect => effect;

    public void Push(AudioReverbEffect dto)
    {
        effect.density = dto.density;
        effect.diffusion = dto.diffusion;
        effect.gain = dto.gain;
        effect.gainHF = dto.gainHF;
        effect.gainLF = dto.gainLF;
        effect.decayTime = dto.decayTime;
        effect.decayHFRatio = dto.decayHFRatio;
        effect.decayLFRatio = dto.decayLFRatio;
        effect.reflectionsGain = dto.reflectionsGain;
        effect.reflectionsDelay = dto.reflectionsDelay;
        effect.reflectionsPan = dto.reflectionsPan;
        effect.lateReverbGain = dto.lateReverbGain;
        effect.lateReverbDelay = dto.lateReverbDelay;
        effect.lateReverbPan = dto.lateReverbPan;
        effect.echoTime = dto.echoTime;
        effect.echoDepth = dto.echoDepth;
        effect.modulationTime = dto.modulationTime;
        effect.modulationDepth = dto.modulationDepth;
        effect.airAbsorptionGainHF = dto.airAbsorptionGainHF;
        effect.hfReference = dto.hfReference;
        effect.lfReference = dto.lfReference;
        effect.roomRolloffFactor = dto.roomRolloffFactor;
        effect.decayHFLimit = dto.decayHFLimit;
        effect.effectSlotGain = dto.effectSlotGain;

        effect.dirty = true;
        effect.Update();
    }

    public void Dispose() => effect.Dispose();
}
