namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public ALReverbEffect GetReverbEffect(vaudio.Emitter emitter)
    {
        if (emitter.AffectsGroupedEAX && emitter.GroupedEAXIndex >= 0)
        {
            if (emitter.GroupedEAXIndex >= groupedReverbEffects.Count)
            {
                LogWarning($"Emitter {emitter.Name} has a grouped EAX index of {emitter.GroupedEAXIndex} but only {groupedReverbEffects.Count} EAX presets are available.");
                return listenerReverbEffect;
            }

            return groupedReverbEffects[emitter.GroupedEAXIndex];
        }

        return listenerReverbEffect;
    }

    public ALReverbEffect GetReverbEffect(VAEmitter emitter)
    {
        if (emitter.AffectsGroupedEAX && emitter.GroupedEAXIndex >= 0)
        {
            if (emitter.GroupedEAXIndex >= groupedReverbEffects.Count)
            {
                LogWarning($"Emitter {emitter.Name} has a grouped EAX index of {emitter.GroupedEAXIndex} but only {groupedReverbEffects.Count} EAX presets are available.");
                return listenerReverbEffect;
            }

            return groupedReverbEffects[emitter.GroupedEAXIndex];
        }

        // Doesn't cast reverb rays or affect a grouped EAX zone - falls back to the listener's
        // reverb effect only if this emitter opted into that via UseListenerReverb, otherwise
        // it gets no reverb send at all.
        if (!emitter.UseListenerReverb)
            return null;

        return listenerReverbEffect;
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

            if (ALManager.Initialised)
            {
                ambientFilter ??= new(ambientGainLF, ambientGainHF);
                ambientFilter.SetGain(ambientGainLF, ambientGainHF);
            }
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

    // Grouped-EAX reverb zones get a listener-relative pan (and effect-slot gain) applied to their
    // reflections/late-reverb. The pan math is dimension-specific - OpenAL's pan vector is always
    // 3-component, but 2D derives it from a single listener rotation while 3D uses pitch + yaw - so
    // each addon supplies its own implementation.
    partial void ApplyGroupedEAXPan(vaudio.EAXReverb eax, ALReverbEffect effect);
}
