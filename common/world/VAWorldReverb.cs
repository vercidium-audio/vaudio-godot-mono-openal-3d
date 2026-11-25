// Human-created

namespace vaudio_godot_mono_openal;

// A grouped EAX's reverb bus, with the gain and pan last calculated for it, which it keeps while its EAX has no listener-relative results
internal class VAGroupedReverbBus(VAReverbBus bus)
{
    public readonly VAReverbBus Bus = bus;
    public float Gain = 1;
    public Vector3 Pan;
}

public partial class VAWorld
{
    // Grouped reverb buses are only created once reverb updates with a listener
    void WarnMissingGroupedEAX(string emitterName, int groupedEAXIndex)
    {
        if (listener == null)
            LogWarning($"Emitter {emitterName} has a grouped EAX index of {groupedEAXIndex} but the VAWorld has no VAListener. Ensure the VAListener node is created and is a sibling or child node of VAWorld.");
        else
            LogWarning($"Emitter {emitterName} has a grouped EAX index of {groupedEAXIndex} but only {groupedReverbBuses.Count} EAX presets are available. Increase the 'Maximum Grouped EAX Count' setting on the VAWorld node.");
    }

    public VAReverbBus GetReverbBus(VAEmitter emitter)
    {
        // Not raytraced outside the bounds, so any grouped EAX it has is stale, and the listener's reverb doesn't describe where it is either
        if (emitter.IsOutsideBounds())
            return null;

        if (emitter.AffectsGroupedEAX && emitter.GroupedEAXIndex >= 0)
        {
            if (emitter.GroupedEAXIndex >= groupedReverbBuses.Count)
            {
                WarnMissingGroupedEAX(emitter.Name, emitter.GroupedEAXIndex);
                return listenerReverbBus;
            }

            return groupedReverbBuses[emitter.GroupedEAXIndex].Bus;
        }

        if (!emitter.UseListenerReverb)
            return null;

        return listenerReverbBus;
    }

    // Whether the current listener's reverb bus has its first EAX, or never will: no reverb bus, no reverb rays, or outside the bounds. False with no listener. VASourceRelative waits for this
    internal bool IsListenerReverbReady()
    {
        if (listener?.emitter == null)
            return false;

        return listenerReverbBus == null || !listenerReverbBus.IsValid || !listener.IsWaitingForEAX() || listener.IsOutsideBounds();
    }

    // Warns once per source (warned tracks it) that uses the listener's reverb bus while the current listener casts no reverb rays, as it then plays without reverb
    internal void WarnIfListenerCastsNoReverbRays(Node source, ref bool warned)
    {
        if (warned || listener?.emitter == null || listener.emitter.ReverbEnabled)
            return;

        warned = true;
        LogWarning($"{source.Name}: The listener '{listener.Name}' casts no reverb rays, so this source plays without the listener's reverb. Set the listener's reverb ray and bounce counts above 0.");
    }

    void OnReverbUpdated()
    {
        raytraceCount++;
        VAAudioBackend.Ensure();

        // Shouldn't access anything until user adds a VAListener to the scene - else what is reverb relative to?
        if (listener == null)
            return;

        // Update ambient gain
        if (listener.AmbientFilter != null)
            ambientFilter = new VALowPass(listener.AmbientFilter.GainLF, listener.AmbientFilter.GainHF);

        // Copy raytraced EAX results to the reverb buses
        if (listener.EAX != null)
            listenerReverbBus?.SetParams(listener.EAX, 1, Vector3.Zero, Vector3.Zero);

        var backend = VAAudioBackend.Backend;

        for (int i = 0; i < world.GroupedEAX.Count; i++)
        {
            if (groupedReverbBuses.Count <= i)
            {
                if (backend == null)
                    break;

                groupedReverbBuses.Add(new(backend.CreateReverbBus()));
            }

            var eax = world.GroupedEAX[i];
            var grouped = groupedReverbBuses[i];

            if (eax.HasRelativeResults(listener.emitter))
            {
                grouped.Gain = Math.Clamp(eax.GetRelativeGain(listener.emitter), 0, 1);
                grouped.Pan = GetGroupedEAXPan(eax);
            }

            // TODO - separate pan for late reverb and reflections
            grouped.Bus.SetParams(eax, grouped.Gain, grouped.Pan, grouped.Pan);
        }
    }

    void OnDeviceRecreated()
    {
        // The buses belong to the device, so they're created again with it
        listenerReverbBus = VAAudioBackend.Backend?.CreateReverbBus();
    }

    void OnDeviceDestroyed()
    {
        ambientFilter = null;

        listenerReverbBus?.Dispose();
        listenerReverbBus = null;

        foreach (var grouped in groupedReverbBuses)
            grouped.Bus.Dispose();

        groupedReverbBuses.Clear();
    }
}
