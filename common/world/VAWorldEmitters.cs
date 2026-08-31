namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public List<vaudio.Emitter> emitters = [];

    // Emitters that registered before the listener existed - added to the listener's targets once it registers
    List<vaudio.Emitter> pendingTargets = [];

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

                // Wire up any emitters that registered before this listener existed
                foreach (var pendingTarget in pendingTargets)
                    listener.AddTarget(pendingTarget);

                pendingTargets.Clear();
            }
            else
            {
                LogWarning($"The {listener.Name} node is already the VAListener, but {node.Name} is also a VAListener. Only one VAListener node is allowed");
            }
        }
        else
        {
            if (listener == null)
            {
                // List of emitters to initialise later when the VAListener node is created
                pendingTargets.Add(emitter);
            }
            else
            {
                listener.AddTarget(emitter);
            }
        }

        emitters.Add(emitter);
        return emitter;
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

    public void UnregisterPendingTarget(vaudio.Emitter emitter) => pendingTargets.Remove(emitter);

    public void UnregisterListener(VAEmitter node)
    {
        if (listener != node)
            return;

        listener = null;
        NoListenerWarningLogged = false;
    }
}
