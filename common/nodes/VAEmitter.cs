namespace vaudio_godot_mono_openal;

// Base type (Node2D/Node3D) and [Tool]/[GlobalClass] are declared per-addon in VAEmitterBase.cs.
public partial class VAEmitter
{
    public bool IsMainListener => this is VAListener;

    protected VAWorld vercidiumAudio;
    public vaudio.Emitter emitter;

    public VAReverbBus reverbBus;
    VALowPass? filter;

    public float GainLF => filter?.GainLF ?? 0;
    public float GainHF => filter?.RelativeGainHF ?? 0;

    // How muffled this emitter sounds to the listener, as absolute gains (GainHF is relative to GainLF). 1/1 until the listener raytraces it
    internal float MufflingGainLF { get; private set; } = 1;
    internal float MufflingGainHF { get; private set; } = 1;
    public bool Raytraced => emitter != null && !emitter.Initialising;

    // Set while waiting for a VAWorld to appear. _ExitTree cancels the pending retry if this node leaves the tree
    Action cancelWaitForVAWorld;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
            return;

        VAAudioBackend.Ensure();

        cancelWaitForVAWorld = this.WaitForVAWorld(OnVAWorldFound);
    }

    // Set from leaving the tree until the end of that frame. If this node re-enters the same VAWorld before then (e.g. a reparent), it keeps its emitter
    bool detachPending = false;

    void OnVAWorldFound(VAWorld world)
    {
        cancelWaitForVAWorld = null;

        if (detachPending)
        {
            if (world == vercidiumAudio)
            {
                detachPending = false;

                // Follow this node again, as its position was frozen while it was out of the tree
                if (emitter != null)
                    vercidiumAudio.BindEmitterPosition(this, emitter);

                return;
            }

            ReleaseFromWorld();
        }

        vercidiumAudio = world;

        AttachToWorld();
    }

    void DeferRelease()
    {
        detachPending = true;

        // Stop reading this node's position while it's out of the tree
        if (emitter != null)
            emitter.Position = emitter.Position.GetPosition();

        Callable.From(ReleaseIfPending).CallDeferred();
    }

    void ReleaseIfPending()
    {
        if (detachPending)
            ReleaseFromWorld();
    }

    void ReleaseFromWorld()
    {
        detachPending = false;

        // The VAWorld may have left the tree first (e.g. a scene change), which disposed its world and every emitter in it
        if (IsInstanceValid(vercidiumAudio) && vercidiumAudio.IsInsideTree())
        {
            DetachFromWorld();
        }
        else
        {
            TeardownVisualisation();
            emitter = null;
        }

        vercidiumAudio = null;

        // Creating a fresh emitter creates a fresh filter once it's raytraced
        filter = null;
    }

    public override void _Notification(int what)
    {
        // Freed before the end of the frame it left the tree in
        if (what == NotificationPredelete && detachPending)
            ReleaseFromWorld();
    }

    // Listeners override these to go through VAWorld's shared listener emitter instead of owning one
    protected virtual void AttachToWorld() => CreateEmitter();

    protected virtual void DetachFromWorld()
    {
        if (emitter == null)
            return;

        vercidiumAudio.UnregisterPendingTarget(emitter);
        ReleaseEmitter();
    }

    // Compatibility shim: forwards the pre-1.9.0 "RefreshRayCount" export to TrailRefreshCount so existing .tscn/.tres files keep loading. It's not in the property list, so the inspector doesn't show it and Godot rewrites the scene to the new name on next save.
    public override bool _Set(StringName property, Variant value)
    {
        if (property == "RefreshRayCount")
        {
            TrailRefreshCount = value.AsInt32();
            return true;
        }

        return base._Set(property, value);
    }

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        base._ValidateProperty(property);

        string name = property["name"].AsStringName();

        // Occlusion/permeation rays are only cast by the listener, so their thresholds are only customisable there
        if (this is not VAListener && (name == "MinimumOcclusionEnergy" || name == "MinimumPermeationEnergy" || name == "TargetClusteringEnabled" || name == "TargetClusteringDistance"))
        {
            var usage = property["usage"].As<PropertyUsageFlags>();
            usage &= ~PropertyUsageFlags.Editor;
            property["usage"] = (int)usage;
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var sceneRoot = Engine.IsEditorHint() ? GetTree()?.EditedSceneRoot : GetTree()?.CurrentScene;
        if (sceneRoot == null)
            return [];

        // A VAWorld can be found anywhere or added later, so this is just a hint, not a hard requirement
        if (sceneRoot.GetVAWorldParent() == null)
            return ["No VAWorld node found in the scene tree."];

        return [];
    }

    public void CreateEmitter()
    {
        if (emitter != null)
            throw new InvalidOperationException("Emitter already created");

        emitter = vercidiumAudio.CreateEmitter(this, OnRaytracingComplete, OnRaytracedByAnotherEmitter);
        raytraceOnceRemoved = false;
        SetOnRemoved(emitter);

        // A visualisation that entered the tree while this node had no emitter
        InitializeVisualisation();
    }

    // The VAVisualisation attached to this node, if any
    internal VAVisualisation visualisation;

    // Starts the attached VAVisualisation, once this node holds an emitter
    internal void InitializeVisualisation() => visualisation?.InitializeVisualisation();

    // Stops it. Call before this node lets go of its emitter
    internal void TeardownVisualisation() => visualisation?.TeardownVisualisation();

    protected void SetOnRemoved(vaudio.Emitter created) => created.OnRemoved = () => OnEmitterRemoved(created);

    // Re-points an existing emitter at this node (position, settings and callbacks)
    protected void ConfigureEmitter(vaudio.Emitter existing)
    {
        vercidiumAudio.ConfigureEmitter(this, existing, OnRaytracingComplete, OnRaytracedByAnotherEmitter);
        SetOnRemoved(existing);
    }

    public void RemoveEmitter()
    {
        if (emitter == null)
            throw new InvalidOperationException("Emitter already removed");

        // Don't null out emitter here, need to wait for its pending reverb tail to finish
        vercidiumAudio.RemoveEmitter(emitter);
    }

    // Removes the emitter from the world and detaches it from this node, even if its removal is still pending (reverb tail), so re-entering the tree creates a fresh one
    protected void ReleaseEmitter()
    {
        TeardownVisualisation();
        RemoveEmitter();
        emitter = null;
    }

    void OnEmitterRemoved(vaudio.Emitter removed)
    {
        vercidiumAudio?.UnregisterPendingTarget(removed);

        // May be an older emitter than this node's current one, e.g. after moving to a different VAWorld while a reverb tail was still playing. Only the current one's removal is reported, or a stale tail finishing would tear down the node's new emitter
        if (emitter != removed)
            return;

        emitter = null;

        // This node may have been freed while the reverb tail finished
        if (IsInstanceValid(this))
        {
            TeardownVisualisation();
            OnEmitterRemovedCallback?.Invoke();
        }
    }

    void OnRaytracingComplete()
    {
        OnRaytracingCompleteCallback?.Invoke();
    }

    void OnRaytracedByAnotherEmitter(vaudio.Emitter emitter)
    {
        VAAudioBackend.Ensure();

        // Already set if a new listener emitter raytraced this again, e.g. after the last VAListener left the tree and came back
        filter ??= VALowPass.Full;

        ApplyRaytracingResults();

        OnRaytracedByAnotherEmitterCallback?.Invoke(emitter);
    }

    bool raytraceOnceRemoved = false;

    bool warnedNoListenerReverb = false;

    public override void _Process(double delta)
    {
        // If initialisation failed, skip
        if (emitter == null)
            return;

        if (Raytraced)
            ApplyRaytracingResults();

        if (UseListenerReverb)
            vercidiumAudio?.WarnIfListenerCastsNoReverbRays(this, ref warnedNoListenerReverb);

        // Once ready, cast rays no more. A parent VARaytracedSource processes before this node, so it has already played. A sound started outside the bounds keeps going until it's raytraced inside and has reverb
        if (RaytraceOnce && !raytraceOnceRemoved && HasListenerResults())
        {
            raytraceOnceRemoved = true;

            // The listener stops raytracing it too, so its muffling is held at this result, even while its reverb tail keeps it in the world
            vercidiumAudio.listener.RemoveTarget(emitter);
            RemoveEmitter();
        }
    }

    // The listener has raytraced it and, if waitForOwnEAX, it has cast its own reverb rays for grouped EAX
    bool HasListenerResults(bool waitForOwnEAX)
    {
        var listener = vercidiumAudio?.listener;

        if (emitter == null || listener == null || listener == this)
            return false;

        // A raytrace-once emitter has left the listener's targets and holds its last muffling
        if (!raytraceOnceRemoved && !listener.HasRaytracedTarget(this))
            return false;

        // Its grouped EAX slot, and so its reverb effect, isn't known until it casts its own reverb rays
        return !(waitForOwnEAX && emitter.AffectsGroupedEAX && IsWaitingForEAX());
    }

    // Casts reverb rays but has no EAX yet
    internal bool IsWaitingForEAX() => emitter != null && emitter.ReverbEnabled && emitter.EAX == null;

    // Sent to the listener's reverb effect (see VAWorld.GetReverbBus), which has no EAX yet
    bool IsWaitingForListenerReverb() => UseListenerReverb && !(AffectsGroupedEAX && GroupedEAXIndex >= 0) && !vercidiumAudio.IsListenerReverbReady();

    /// <summary>
    /// True once this emitter has cast its own rays and the listener has raytraced it. Sources don't play until this is true. Inside the bounds it also waits for its own grouped EAX, and with <see cref="UseListenerReverb"/> for the listener's first EAX, neither of which exist outside them
    /// </summary>
    public bool IsReadyToPlay
    {
        get
        {
            if (!Raytraced)
                return false;

            var insideBounds = !IsOutsideBounds();
            return HasListenerResults(insideBounds) && !(insideBounds && IsWaitingForListenerReverb());
        }
    }

    /// <summary>
    /// <see cref="IsReadyToPlay"/>, but always waiting for its own reverb rays. <see cref="RaytraceOnce"/> waits for this, so a sound started outside the bounds still gets reverb once inside
    /// </summary>
    public bool HasListenerResults() => HasListenerResults(true);

    /// <summary>
    /// Unclamped and outside the VAWorld's bounds, so the SDK doesn't raytrace it and it has no reverb
    /// </summary>
    public bool IsOutsideBounds() => vercidiumAudio != null && !ClampPosition && !vercidiumAudio.IsWithinBounds(GlobalPosition);

    /// <summary>
    /// Outside the bounds while <see cref="VAWorld.EmittersOutsideTheWorldAreMuffled"/> is on. Sources don't start or stop while this is true
    /// </summary>
    public bool IsSilencedByBounds() => vercidiumAudio != null && vercidiumAudio.EmittersOutsideTheWorldAreMuffled && IsOutsideBounds();

    void ApplyRaytracingResults()
    {
        reverbBus = vercidiumAudio.GetReverbBus(this);

        // If no listener, we can't know how muffled we are
        if (vercidiumAudio.listener == null)
            return;

        // Apply filter if we aren't the listener
        if (this != vercidiumAudio.listener)
        {
            if (vercidiumAudio.listener.HasRaytracedTarget(this))
            {
                var vaudioFilter = vercidiumAudio.listener.GetTargetFilter(this);
                MufflingGainLF = vaudioFilter.GainLF;
                MufflingGainHF = vaudioFilter.GainHF;

                if (filter != null)
                    filter = new VALowPass(vaudioFilter.GainLF, vaudioFilter.GainHF);
            }
        }
    }

    public bool HasRaytracedTarget(VAEmitter target) => emitter.HasTarget(target.emitter) && emitter.HasRaytracedTarget(target.emitter);
    public vaudio.LowPassFilter GetTargetFilter(VAEmitter target) => emitter.GetTargetFilter(target.emitter);
    public vaudio.LowPassFilter GetTargetFilter(vaudio.Emitter target) => emitter.GetTargetFilter(target);

    public override void _ExitTree()
    {
        if (cancelWaitForVAWorld != null)
        {
            cancelWaitForVAWorld();
            cancelWaitForVAWorld = null;

            LogWarning($"'{Name}' left the tree without ever finding a VAWorld - no emitter was created for it. Make sure this node's scene was added under a VAWorld while it was in the tree.");
        }

        if (vercidiumAudio != null && !detachPending)
        {
            // A reparent re-enters the tree straight away, so keep the emitter until the end of the frame in case it comes back to the same VAWorld. A raytrace-once emitter that has started its removal can't be reused, and a listener hands the shared emitter over to the next listener straight away
            if (IsQueuedForDeletion() || raytraceOnceRemoved || IsMainListener)
                ReleaseFromWorld();
            else
                DeferRelease();
        }

        base._ExitTree();
    }

    public void AddTarget(vaudio.Emitter target)
    {
        if (!emitter.OcclusionEnabled && !emitter.PermeationEnabled)
        {
            LogError($"VAListener '{Name}' cannot determine how muffled other sounds are. Increase its occlusion or permeation ray counts and try again.");
            return;
        }
        
        emitter.AddTarget(target);
    }

    public void RemoveTarget(vaudio.Emitter target)
    {
        emitter.RemoveTarget(target);
    }

    // Shortcuts
    public vaudio.ProcessedReverb ProcessedReverb => emitter.ProcessedReverb;
    public vaudio.EAXReverb EAX => emitter.EAX;
    public vaudio.LowPassFilter AmbientFilter => emitter.AmbientFilter;
    public int GroupedEAXIndex => emitter?.GroupedEAXIndex ?? -1;

    public bool IsAmbientFilterReady => Raytraced && AmbientFilter != null;
    public float GetAmbientFilterGainLF() => AmbientFilter?.GainLF ?? 1.0f;
    public float GetAmbientFilterGainHF() => AmbientFilter?.GainHF ?? 1.0f;

    public Action OnRaytracingCompleteCallback;
    public Action<vaudio.Emitter> OnRaytracedByAnotherEmitterCallback;
    public Action OnEmitterRemovedCallback;
}
