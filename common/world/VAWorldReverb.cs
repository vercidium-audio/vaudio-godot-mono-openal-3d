namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public IAudioReverbSlot GetReverbEffect(vaudio.Emitter emitter)
    {
        if (emitter.AffectsGroupedEAX && emitter.GroupedEAXIndex >= 0)
        {
            if (emitter.GroupedEAXIndex >= groupedReverbSlots.Count)
            {
                LogWarning($"Emitter {emitter.Name} has a grouped EAX index of {emitter.GroupedEAXIndex} but only {groupedReverbSlots.Count} EAX presets are available.");
                return listenerReverbSlot;
            }

            return groupedReverbSlots[emitter.GroupedEAXIndex];
        }

        return listenerReverbSlot;
    }

    public IAudioReverbSlot GetReverbEffect(VAEmitter emitter)
    {
        if (emitter.AffectsGroupedEAX && emitter.GroupedEAXIndex >= 0)
        {
            if (emitter.GroupedEAXIndex >= groupedReverbSlots.Count)
            {
                LogWarning($"Emitter {emitter.Name} has a grouped EAX index of {emitter.GroupedEAXIndex} but only {groupedReverbSlots.Count} EAX presets are available.");
                return listenerReverbSlot;
            }

            return groupedReverbSlots[emitter.GroupedEAXIndex];
        }

        if (!emitter.UseListenerReverb)
            return null;

        return listenerReverbSlot;
    }

    void OnReverbUpdated()
    {
        // Shouldn't access anything until user adds a VAListener to the scene - else what is reverb relative to?
        if (listener == null)
            return;

        // Update ambient gain (if reverb enabled)
        if (listener.AmbientFilter != null)
        {
            var ambientGainLF = listener.AmbientFilter.GainLF;
            var ambientGainHF = listener.AmbientFilter.GainHF;

            if (AudioManager.Initialised)
            {
                ambientFilter ??= AudioManager.Backend.CreateFilter(ambientGainLF, ambientGainHF);
                ambientFilter.SetGain(ambientGainLF, ambientGainHF);
            }
        }

        // Apply raytraced EAX results to the reverb slots
        if (listener.EAX != null && listenerReverbSlot != null)
            CopyReverb(listener.EAX, listenerReverbEffect, listenerReverbSlot, false);

        for (int i = 0; i < world.GroupedEAX.Count; i++)
        {
            if (groupedReverbSlots.Count <= i)
            {
                groupedReverbSlots.Add(AudioManager.Backend.CreateReverbSlot());
                groupedReverbEffects.Add(new());
            }

            CopyReverb(world.GroupedEAX[i], groupedReverbEffects[i], groupedReverbSlots[i], true);
        }
    }

    void CopyReverb(vaudio.EAXReverb eax, AudioReverbEffect effect, IAudioReverbSlot slot, bool isGroupedEAX)
    {
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

        if (isGroupedEAX)
            ApplyGroupedEAXPan(eax, effect);

        effect.dirty = true;
        slot.Push(effect);
    }

    partial void ApplyGroupedEAXPan(vaudio.EAXReverb eax, AudioReverbEffect effect);
}
