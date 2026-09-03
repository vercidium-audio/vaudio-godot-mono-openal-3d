namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public List<vaudio.Emitter> emitters = [];

    // Contains every non-listener emitter. When the listener is finally added, this list is processed
    List<vaudio.Emitter> registeredEmitters = [];

    bool wirePendingTargetsQueued = false;

    public vaudio.Emitter CreateEmitter(VAEmitter node, Action OnRaytracingComplete, Action<vaudio.Emitter> OnRaytracedByAnotherEmitter)
    {
        // RaytraceOnce emitters cast their rays once and are done — freeze their position at that
        // moment (e.g. a dodgeball impact SFX) instead of tracking the node forever afterward.
        var position = node.RaytraceOnce
            ? (vaudio.IPosition)ToVAudio(node.GlobalPosition)
            : new vaudio.FuncPosition(() => ToVAudio(node.GlobalPosition));

        var emitter = new vaudio.Emitter
        {
            Name = node.Name,
            Position = position,
            OnRaytracingComplete = OnRaytracingComplete,
            OnRaytracedByAnotherEmitter = OnRaytracedByAnotherEmitter,

            // Reverb
            ReverbRayCount = node.ReverbRayCount,
            ReverbBounceCount = node.ReverbBounceCount,
            ReverbEnergyCap = node.ReverbEnergyCap,
            MaxVolume = node.MaxVolume,
            MaxEchogramTime = node.MaxEchogramTime,
            EchogramGranularity = node.EchogramGranularity,
            AffectsGroupedEAX = node.AffectsGroupedEAX,
            HasRelativeReverb = node.HasRelativeReverb,
            RelativeReverbInnerThreshold = node.RelativeReverbInnerThreshold,
            RelativeReverbOuterThreshold = node.RelativeReverbOuterThreshold,

            // Muffling
            OcclusionRayCount = node.OcclusionRayCount,
            OcclusionBounceCount = node.OcclusionBounceCount,
            PermeationRayCount = node.PermeationRayCount,
            PermeationBounceCount = node.PermeationBounceCount,
            OcclusionEnergyCap = node.OcclusionEnergyCap,
            PermeationEnergyCap = node.PermeationEnergyCap,

            // Ambience
            AmbientOcclusionRayCount = node.AmbientOcclusionRayCount,
            AmbientOcclusionBounceCount = node.AmbientOcclusionBounceCount,
            AmbientPermeationRayCount = node.AmbientPermeationRayCount,
            AmbientPermeationBounceCount = node.AmbientPermeationBounceCount,
            AmbientOcclusionEnergyCap = node.AmbientOcclusionEnergyCap,
            AmbientPermeationEnergyCap = node.AmbientPermeationEnergyCap,

            // Debug rendering
            RandomTrailColor = node.RandomTrailColor,
            TrailColor = ToVAudio(node.TrailColor),
            OcclusionColor = ToVAudio(node.OcclusionColor),
            PermeationColor = ToVAudio(node.PermeationColor),
            AmbientPermeationColor = ToVAudio(node.AmbientPermeationColor),

            // Advanced
            Type = node.Type,
            RefreshRayCount = node.RefreshRayCount,
            RefreshDistanceThreshold = node.RefreshDistanceThreshold,
            ScatteringSeed = node.ScatteringSeed,
            ClampPosition = node.ClampPosition,
        };

        world.AddEmitter(emitter);

        if (node.IsMainListener)
        {
            if (listener == null)
            {
                listener = node;

                // Set up the sources that were created before the listener existed
                WirePendingTargets();
            }
            else
            {
                LogWarning($"The {listener.Name} node is already the VAListener, but {node.Name} is also a VAListener. Only one VAListener node is allowed");
            }
        }
        else
        {
            // Keep track of all emitters
            registeredEmitters.Add(emitter);

            if (listener != null)
            {
                listener.AddTarget(emitter);
            }
            else if (!wirePendingTargetsQueued)
            {
                // If this node was added before the VAlistener was created, we need to defer-process all sources/emitters later
                wirePendingTargetsQueued = true;
                Callable.From(WirePendingTargets).CallDeferred();
            }
        }

        emitters.Add(emitter);
        return emitter;
    }

    // Process sources/emitters that were created before the VAListener node was created
    void WirePendingTargets()
    {
        wirePendingTargetsQueued = false;

        if (listener == null)
            return;

        foreach (var emitter in registeredEmitters)
            if (!emitter.PendingRemoval)
                listener.AddTarget(emitter);
    }

    public void RemoveEmitter(vaudio.Emitter emitter)
    {
        Debug.Assert(emitter != null);

        // Ignore if already queued for removal
        if (emitter.PendingRemoval)
            return;

        if (emitter.ReverbEnabled && emitter.AffectsGroupedEAX)
        {
            // Capture the old callback (if any)
            var existingCallback = emitter.OnRemoved;

            emitter.OnRemoved = () =>
            {
                // Remove it once its reverb tail has finished
                emitters.Remove(emitter);
                listener.RemoveTarget(emitter);
                existingCallback?.Invoke();
            };
        }

        world.RemoveEmitter(emitter);
    }

    public void UnregisterPendingTarget(vaudio.Emitter emitter) => registeredEmitters.Remove(emitter);

    public void UnregisterListener(VAEmitter node)
    {
        if (listener != node)
            return;

        listener = null;
        NoListenerWarningLogged = false;
    }
}
