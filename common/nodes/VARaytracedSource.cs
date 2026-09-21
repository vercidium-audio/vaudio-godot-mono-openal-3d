namespace vaudio_godot_mono_openal;

public partial class VARaytracedSource
{
    protected VAWorld vercidiumAudio;
    protected VAEmitter emitter;

    public VAEmitter RaytracedEmitter => emitter;

    public bool Raytraced => emitter != null && emitter.Raytraced;

    // True once the world's listener has actually raytraced this source, i.e. GainLF/GainHF reflect a real listener->source occlusion result rather than the unmuffled default. Stricter than Raytraced.
    public bool IsRaytracedByListener
    {
        get
        {
            if (!Raytraced || vercidiumAudio?.listener == null)
                return false;

            var listener = vercidiumAudio.listener;
            return listener != emitter && listener.HasRaytracedTarget(emitter);
        }
    }

    private bool _RaytraceOnce = false;

    [Export]
    public bool RaytraceOnce
    {
        get => _RaytraceOnce;
        set => _RaytraceOnce = value;
    }

    // Read-only muffling stats - how muffled this source currently is (0..1, 1 = not muffled)
    public float GainLF => emitter?.GainLF ?? 0;
    public float GainHF => emitter?.GainHF ?? 0;

    // Set while waiting for a VAWorld to appear. _ExitTree cancels the pending retry if this node leaves the tree before one is found.
    Action cancelWaitForVAWorld;

    public override void _EnterTree()
    {
        base._EnterTree();

        if (Engine.IsEditorHint())
            return;

        cancelWaitForVAWorld = this.WaitForVAWorld(OnVAWorldFound);
    }

    void OnVAWorldFound(VAWorld world)
    {
        cancelWaitForVAWorld = null;
        vercidiumAudio = world;

        CreateEmitter();
    }

    public override string[] _GetConfigurationWarnings()
    {
        var baseWarnings = base._GetConfigurationWarnings();

        var sceneRoot = Engine.IsEditorHint() ? GetTree()?.EditedSceneRoot : GetTree()?.CurrentScene;
        if (sceneRoot == null)
            return baseWarnings;

        // A VAWorld can be found anywhere or added later, so this is just a hint, not a hard requirement
        if (sceneRoot.GetVAWorldParent() == null)
            return [.. baseWarnings, "No VAWorld node found in the scene tree."];

        return baseWarnings;
    }

    public virtual void CreateEmitter()
    {
        emitter = new VAEmitter()
        {
            Name = $"{Name}-Emitter",
            OnRaytracedByAnotherEmitterCallback = OnRaytracedByAnotherEmitter,
            OnEmitterRemovedCallback = OnEmitterRemoved,
            RaytraceOnce = RaytraceOnce,

            // Reverb
            ReverbRayCount = ReverbRayCount,
            ReverbBounceCount = ReverbBounceCount,
            ReverbEnergyCap = ReverbEnergyCap,
            MaxVolume = MaxVolume,
            MaxEchogramTime = MaxEchogramTime,
            EchogramGranularity = EchogramGranularity,
            AffectsGroupedEAX = AffectsGroupedEAX,
            UseListenerReverb = UseListenerReverb,
            HasRelativeReverb = false,

            // Muffling
            OcclusionRayCount = 0,
            OcclusionBounceCount = 0,
            PermeationRayCount = 0,
            PermeationBounceCount = 0,
            OcclusionEnergyCap = OcclusionEnergyCap,
            PermeationEnergyCap = PermeationEnergyCap,

            // Ambience
            AmbientOcclusionRayCount = AmbientOcclusionRayCount,
            AmbientOcclusionBounceCount = AmbientOcclusionBounceCount,
            AmbientPermeationRayCount = AmbientPermeationRayCount,
            AmbientPermeationBounceCount = AmbientPermeationBounceCount,
            AmbientOcclusionEnergyCap = AmbientOcclusionEnergyCap,
            AmbientPermeationEnergyCap = AmbientPermeationEnergyCap,

            // Advanced
            TrailRefreshCount = TrailRefreshCount,
            RefreshDistanceThreshold = RefreshDistanceThreshold,
            ScatteringSeed = ScatteringSeed,
            ClampPosition = ClampPosition,
        };

        AddChild(emitter);
    }

    protected virtual void OnRaytracedByAnotherEmitter(vaudio.Emitter other)
    {
        ApplyRaytracingResults(other);
    }

    void OnEmitterRemoved()
    {
        Debug.Assert(emitter != null);

        RemoveChild(emitter);
        emitter = null;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        // Even if we have cast our own reverb rays, we also need to wait for the listener to initialise
        if (Raytraced && vercidiumAudio.listener != null)
            ApplyRaytracingResults(vercidiumAudio.listener.emitter);
    }

    protected void ApplyRaytracingResults(vaudio.Emitter other)
    {
        effect = vercidiumAudio.GetReverbEffect(emitter);

        if (other.HasRaytracedTarget(emitter.emitter))
        {
            var vaudioFilter = other.GetTargetFilter(emitter.emitter);
            UpdateFilter(vaudioFilter.GainLF, vaudioFilter.GainHF, true);
        }
    }

    public override void _ExitTree()
    {
        if (cancelWaitForVAWorld != null)
        {
            cancelWaitForVAWorld();
            cancelWaitForVAWorld = null;

            LogWarning($"'{Name}' left the tree without ever finding a VAWorld - no emitter was created for it. Make sure this node's scene was added under a VAWorld while it was in the tree.");
        }

        base._ExitTree();
    }
}
