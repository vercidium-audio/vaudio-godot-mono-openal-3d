namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public List<vaudio.Emitter> emitters = [];

    // Contains every non-listener emitter. When the listener is finally added, this list is processed
    List<vaudio.Emitter> registeredEmitters = [];

    // Every VAListener attached to this world, current or not. They all share one vaudio.Emitter, which is controlled by the current listener. The first becomes current when the current listener leaves the tree.
    List<VAListener> listeners = [];

    bool wirePendingTargetsQueued = false;

    public vaudio.Emitter CreateEmitter(VAEmitter node, Action OnRaytracingComplete, Action<vaudio.Emitter> OnRaytracedByAnotherEmitter)
    {
        var emitter = new vaudio.Emitter();
        ConfigureEmitter(node, emitter, OnRaytracingComplete, OnRaytracedByAnotherEmitter);

        world.AddEmitter(emitter);

        // Listeners are wired up by SetCurrentListener
        if (!node.IsMainListener)
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

    // Also used when a VAListener takes over the shared listener emitter, so the emitter follows and uses the settings of its new node
    public void ConfigureEmitter(VAEmitter node, vaudio.Emitter emitter, Action OnRaytracingComplete, Action<vaudio.Emitter> OnRaytracedByAnotherEmitter)
    {
        // RaytraceOnce emitters cast their rays once and are done — freeze their position at that
        // moment (e.g. a dodgeball impact SFX) instead of tracking the node forever afterward.
        emitter.Position = node.RaytraceOnce
            ? (vaudio.IPosition)ToVAudio(node.GlobalPosition)
            : new vaudio.FuncPosition(() => ToVAudio(node.GlobalPosition));

        emitter.Name = node.Name;
        emitter.OnRaytracingComplete = OnRaytracingComplete;
        emitter.OnRaytracedByAnotherEmitter = OnRaytracedByAnotherEmitter;

        // Reverb
        emitter.ReverbRayCount = node.ReverbRayCount;
        emitter.ReverbBounceCount = node.ReverbBounceCount;
        emitter.ReverbEnergyCap = node.ReverbEnergyCap;
        emitter.MaxVolume = node.MaxVolume;
        emitter.MaxEchogramTime = node.MaxEchogramTime;
        emitter.EchogramGranularity = node.EchogramGranularity;
        emitter.AffectsGroupedEAX = node.AffectsGroupedEAX;
        emitter.HasRelativeReverb = node.HasRelativeReverb;
        emitter.RelativeReverbInnerThreshold = node.RelativeReverbInnerThreshold;
        emitter.RelativeReverbOuterThreshold = node.RelativeReverbOuterThreshold;

        // Muffling
        emitter.OcclusionRayCount = node.OcclusionRayCount;
        emitter.OcclusionBounceCount = node.OcclusionBounceCount;
        emitter.PermeationRayCount = node.PermeationRayCount;
        emitter.PermeationBounceCount = node.PermeationBounceCount;
        emitter.OcclusionEnergyCap = node.OcclusionEnergyCap;
        emitter.PermeationEnergyCap = node.PermeationEnergyCap;

        // Ambience
        emitter.AmbientOcclusionRayCount = node.AmbientOcclusionRayCount;
        emitter.AmbientOcclusionBounceCount = node.AmbientOcclusionBounceCount;
        emitter.AmbientPermeationRayCount = node.AmbientPermeationRayCount;
        emitter.AmbientPermeationBounceCount = node.AmbientPermeationBounceCount;
        emitter.AmbientOcclusionEnergyCap = node.AmbientOcclusionEnergyCap;
        emitter.AmbientPermeationEnergyCap = node.AmbientPermeationEnergyCap;

        // Debug rendering
        emitter.RandomTrailColor = node.RandomTrailColor;
        emitter.TrailColor = ToVAudio(node.TrailColor);
        emitter.OcclusionColor = ToVAudio(node.OcclusionColor);
        emitter.PermeationColor = ToVAudio(node.PermeationColor);
        emitter.AmbientPermeationColor = ToVAudio(node.AmbientPermeationColor);

        // Advanced
        emitter.TrailRefreshCount = node.TrailRefreshCount;
        emitter.RefreshDistanceThreshold = node.RefreshDistanceThreshold;
        emitter.ScatteringSeed = node.ScatteringSeed;
        emitter.ClampPosition = node.ClampPosition;
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

        // The emitter outlives its node while its reverb tail plays, so stop reading the node's position - it may be freed before the tail finishes
        emitter.Position = emitter.Position.GetPosition();

        if (emitter.ReverbEnabled && emitter.AffectsGroupedEAX)
        {
            // Capture the old callback (if any)
            var existingCallback = emitter.OnRemoved;

            emitter.OnRemoved = () =>
            {
                // Remove it once its reverb tail has finished
                emitters.Remove(emitter);
                listener?.RemoveTarget(emitter);
                existingCallback?.Invoke();
            };
        }

        world.RemoveEmitter(emitter);
    }

    public void UnregisterPendingTarget(vaudio.Emitter emitter) => registeredEmitters.Remove(emitter);

    public void RegisterListener(VAListener node)
    {
        listeners.Add(node);

        if (listener == null)
        {
            SetCurrentListener(node);
            return;
        }

        // A listener added with Current=true takes over, e.g. a player scene that was spawned with its VAListener.Current=true
        if (node.Current)
        {
            LogWarning($"VAListener '{node.Name}' has Current enabled, so it replaced '{listener.Name}' as the current listener. Disable Current on listeners that shouldn't take over when added, and call MakeCurrent() on the one that should be used.");
            SetCurrentListener(node);
        }
    }

    public void UnregisterListener(VAListener node)
    {
        listeners.Remove(node);

        if (listener != node)
            return;

        if (listeners.Count > 0)
        {
            SetCurrentListener(listeners[0]);
            return;
        }

        // Last listener in this world, so the shared emitter goes with it
        node.ReleaseSharedEmitter();
        listener = null;
        NoListenerWarningLogged = false;
    }

    // Hands the shared listener emitter over to node
    public void SetCurrentListener(VAListener node)
    {
        if (listener == node)
            return;

        var previous = (VAListener)listener;
        var sharedEmitter = previous?.emitter;

        previous?.Deactivate();

        listener = node;
        node.Activate(sharedEmitter);

        // Set up the sources that were created before the listener existed
        if (sharedEmitter == null)
            WirePendingTargets();
    }

    // If node is the current listener, hands the shared listener emitter over to another attached listener (if any)
    public void ReleaseCurrentListener(VAListener node)
    {
        if (listener != node)
        {
            node.Deactivate();
            return;
        }

        foreach (var other in listeners)
        {
            if (other != node)
            {
                SetCurrentListener(other);
                return;
            }
        }

        LogWarning($"VAListener '{node.Name}' is the only listener in this world, so it stays current.");
    }
}
