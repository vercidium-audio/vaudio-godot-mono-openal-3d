namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public ALReverbEffect GetReverbEffect(vaudio.Emitter emitter)
    {
        if (emitter.AffectsGroupedEAX && emitter.GroupedEAXIndex >= 0)
        {
            if (emitter.GroupedEAXIndex >= groupedReverbEffects.Count)
            {
                WarnMissingGroupedEAX(emitter.Name, emitter.GroupedEAXIndex);
                return listenerReverbEffect;
            }

            return groupedReverbEffects[emitter.GroupedEAXIndex];
        }

        return listenerReverbEffect;
    }

    // Grouped reverb effects are only created once reverb updates with a listener
    void WarnMissingGroupedEAX(string emitterName, int groupedEAXIndex)
    {
        if (listener == null)
            LogWarning($"Emitter {emitterName} has a grouped EAX index of {groupedEAXIndex} but the VAWorld has no VAListener, so no EAX presets are available. Ensure the VAListener node is created and is a sibling or child node of VAWorld.");
        else
            LogWarning($"Emitter {emitterName} has a grouped EAX index of {groupedEAXIndex} but only {groupedReverbEffects.Count} EAX presets are available. Increase the 'Maximum Grouped EAX Count' setting on the VAWorld node.");
    }

    public ALReverbEffect GetReverbEffect(VAEmitter emitter)
    {
        // Not raytraced outside the bounds, so any grouped EAX it has is stale, and the listener's reverb doesn't describe where it is either
        if (emitter.IsOutsideBounds())
            return null;

        if (emitter.AffectsGroupedEAX && emitter.GroupedEAXIndex >= 0)
        {
            if (emitter.GroupedEAXIndex >= groupedReverbEffects.Count)
            {
                WarnMissingGroupedEAX(emitter.Name, emitter.GroupedEAXIndex);
                return listenerReverbEffect;
            }

            return groupedReverbEffects[emitter.GroupedEAXIndex];
        }

        if (!emitter.UseListenerReverb)
            return null;

        return listenerReverbEffect;
    }

    void OnReverbUpdated()
    {
        raytraceCount++;
        ALManager.Ensure();
        
        // Shouldn't access anything until user adds a VAListener to the scene - else what is reverb relative to?
        if (listener == null)
            return;

        // Update ambient gain (if reverb enabled)
        if (listener.AmbientFilter != null)
        {
            var ambientGainLF = listener.AmbientFilter.GainLF;
            var ambientGainHF = listener.AmbientFilter.GainHF;

            ambientFilter ??= new(ambientGainLF, ambientGainHF);
            ambientFilter.SetGain(ambientGainLF, ambientGainHF);
        }

        // Apply raytraced EAX results to ALReverbEffects
        if (listener.EAX != null)
            CopyReverb(listener.EAX, listenerReverbEffect, false);

        for (int i = 0; i < world.GroupedEAX.Count; i++)
        {
            if (groupedReverbEffects.Count <= i)
                groupedReverbEffects.Add(new());

            CopyReverb(world.GroupedEAX[i], groupedReverbEffects[i], true);

            groupedReverbEffects[i].Update();
        }
    }

    void CopyReverb(vaudio.EAXReverb eax, ALReverbEffect effect, bool isGroupedEAX)
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
        effect.Update();
    }

    partial void ApplyGroupedEAXPan(vaudio.EAXReverb eax, ALReverbEffect effect);
}
