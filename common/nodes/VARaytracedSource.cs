namespace vaudio_godot_mono_openal;

// Shared raytracing surface for any spatialised source that casts its own reverb/ambience/
// visualisation rays, regardless of how it gets audio into OpenAL (a fixed decoded buffer for
// VASource, a live pushed/callback buffer for VAStreamSource). Factored out of what used to be
// VASource's own private child-VAEmitter machinery so both classes get identical
// reverb/muffling/ambience behaviour instead of duplicating ~25 properties - like an ALSource and
// an ALStreamSource both having all the AL stuff, then either being wired into a VASource or
// VAStreamSource - they're pretty much the same from the VA perspective, it's just that they have
// a different AL base class that does the playback.
//
// Deliberately owns nothing about *when*/*how* playback starts (that's still each subclass's own
// Play()/OpenStream() override) - only the emitter lifecycle, its exported tuning-knob property
// surface, and per-frame application of the listener's raytracing results onto this node's
// filter/reverb slot (inherited from ALSource).
// Not [GlobalClass] - matches native's register_abstract_class<VARaytracedSource>(), which keeps
// it out of the editor's Create Node dialog since it's only meant to be used via a subclass (e.g.
// VASource, VAStreamSource).
// Base type (ALSource2D/ALSource3D) and [Tool] are declared per-addon in VARaytracedSourceBase.cs.
public partial class VARaytracedSource
{
    protected VAWorld vercidiumAudio;
    protected VAEmitter emitter;

    // The internal child VAEmitter this source creates at runtime (see CreateEmitter). Null until
    // then. Exposed so a VAVisualisation placed directly under this source can bind to it without
    // being reparented onto it - keeping the VAVisualisation at its authored scene path so the
    // editor's Sync Scene Changes can forward runtime property edits to it.
    public VAEmitter RaytracedEmitter => emitter;

    public bool Raytraced => emitter != null && emitter.Raytraced;

    private bool _RaytraceOnce = false;

    [Export]
    public bool RaytraceOnce
    {
        get => _RaytraceOnce;
        set => _RaytraceOnce = value;
    }

    // Read-only muffling stats
    public float MufflingGainLF => emitter?.GainLF ?? 0;
    public float MufflingGainHF => emitter?.GainHF ?? 0;

    // Set while waiting for a VAWorld to appear - lets _ExitTree cancel the pending retry if this
    // node leaves the tree before one is found.
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

        // A VAWorld can be found anywhere in the tree, or added later from another scene - see
        // NodeExtensions.GetVAWorldParent/WaitForVAWorld - so this is just a hint, not a hard
        // requirement, and doesn't check tree order.
        if (sceneRoot.GetVAWorldParent() == null)
            return [.. baseWarnings, "No VAWorld node found in the scene tree."];

        return baseWarnings;
    }

    // Matches VASource.cs's CreateEmitter: a private child VAEmitter, never the listener, that
    // never casts its own occlusion/permeation rays (only reverb rays) - this node is heard via
    // the listener's rays targeting it, not by casting its own muffling rays.
    // Virtual so subclasses (e.g. VASource) can apply their own extra properties (e.g. Debug
    // Rendering colors) onto the emitter once it exists.
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
            Type = Type,
            RefreshRayCount = RefreshRayCount,
            RefreshDistanceThreshold = RefreshDistanceThreshold,
            ScatteringSeed = ScatteringSeed,
            ClampPosition = ClampPosition,
        };

        AddChild(emitter);

        // A VAVisualisation placed directly under this source stays where the user put it and binds
        // to the emitter above via RaytracedEmitter - it is not reparented onto the internal
        // VAEmitter, so its scene path is stable and the editor can push runtime property edits to
        // it (Debug > Sync Scene Changes).
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

        if (Raytraced)
            ApplyRaytracingResults(vercidiumAudio.listener.emitter);
    }

    // Resolves this node's reverb slot via its own child emitter's AffectsGroupedEAX/
    // GroupedEAXIndex (VAWorld.GetReverbEffect - the listener slot if this node doesn't cast
    // reverb rays into a grouped zone), then - if the listener has raytraced this node's emitter
    // as a target - pushes the resulting muffling gain with fullReverb=true (reverb send always
    // gets the clean/unfiltered signal; only the direct path is muffled).
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
