namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public List<vaudio.Emitter> emitters = [];

    // Every non-listener emitter currently registered with this world, in registration order. Re-walked to wire listener targets whenever the listener appears (or re-appears after a scene reload), so VASource/VAListener/VAWorld can enter the tree in any order.
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

                // Wire up every source registered so far, in either order - some may have registered before this listener existed
                WirePendingTargets();
            }
            else
            {
                LogWarning($"The {listener.Name} node is already the VAListener, but {node.Name} is also a VAListener. Only one VAListener node is allowed");
            }
        }
        else
        {
            // Recorded before the listener check so a listener whose CreateEmitter runs later this same frame still sees this source when it walks registeredEmitters.
            registeredEmitters.Add(emitter);

            if (listener != null)
            {
                listener.AddTarget(emitter);
            }
            else if (!wirePendingTargetsQueued)
            {
                // No listener registered yet. If a VAListener node is in the tree but its own CreateEmitter simply hasn't run this frame (child-emitter registration order vs sibling _EnterTree), the walk it does on registering already covers us. This deferred re-walk is the belt-and-braces path for any other ordering where neither immediate wiring nor the listener's walk reached this source.
                wirePendingTargetsQueued = true;
                Callable.From(WirePendingTargets).CallDeferred();
            }
        }

        emitters.Add(emitter);
        return emitter;
    }

    // (Re-)adds every registeredEmitters entry as a target of the current listener. Safe to call repeatedly - vaudio.Emitter.AddTarget treats an existing target as a no-op.
    void WirePendingTargets()
    {
        wirePendingTargetsQueued = false;

        if (listener == null)
            return;

        foreach (var emitter in registeredEmitters)
        {
            // Skip a source whose SDK handle has already been torn down (RaytraceOnce removal, pending destroy) but whose node is still briefly in registeredEmitters.
            if (!emitter.PendingRemoval)
                listener.AddTarget(emitter);
        }
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
