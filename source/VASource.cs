using godot_openal;
using System.Linq;

namespace vaudio_godot_openal;

[Tool]
[GlobalClass]
public partial class VASource : ALSource3D
{
    private VAWorld vercidiumAudio;
    VAEmitter emitter;

    public bool Raytraced => emitter != null && emitter.Raytraced;

    private bool _PlayWhenRaytracingCompletes = true;
    private bool _RaytraceOnce = true;
    private bool _wasPlayingBeforeDeviceDestroyed = false;

    [Export]
    public bool PlayWhenRaytracingCompletes
    {
        get => _PlayWhenRaytracingCompletes;
        set => _PlayWhenRaytracingCompletes = value;
    }

    [Export]
    public bool RaytraceOnce
    {
        get => _RaytraceOnce;
        set => _RaytraceOnce = value;
    }

    public override void _EnterTree()
    {
        vercidiumAudio = this.GetVAWorldParent();

        if (!Engine.IsEditorHint())
        {
            // Register for a callback to re-play sounds when changing devices
            RegisterDeviceRecreatedCallback(OnDeviceRecreated);
        }

        // Must create the emitter after the parent VAWorld node is initialised
        CreateEmitter();
    }

    public override string[] _GetConfigurationWarnings()
    {
        var baseWarnings = base._GetConfigurationWarnings();

        var sceneRoot = Engine.IsEditorHint() ? GetTree()?.EditedSceneRoot : GetTree()?.CurrentScene;
        if (sceneRoot == null)
            return baseWarnings;

        var vercidiumAudioNode = sceneRoot.GetChildren().OfType<VAWorld>().FirstOrDefault();
        if (vercidiumAudioNode == null)
            return [.. baseWarnings, "No VAWorld node found. Ensure a VAWorld node exists higher up the tree."];

        // Find the top-level ancestor of this node (direct child of scene root)
        var ancestor = this as Node;
        while (ancestor.GetParent() != sceneRoot)
            ancestor = ancestor.GetParent();

        if (ancestor.GetIndex() < vercidiumAudioNode.GetIndex())
            return [.. baseWarnings, "This node must be lower in the scene tree than the VAWorld node."];

        return baseWarnings;
    }

    public void CreateEmitter()
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

            // Visualisation
            VisualisationRayCount = VisualisationRayCount,
            VisualisationBounceCount = VisualisationBounceCount,
            VisualisationUpdateFrequency = VisualisationUpdateFrequency,

            // Debug rendering
            TrailColor = TrailColor,
            OcclusionColor = OcclusionColor,
            PermeationColor = PermeationColor,
            AmbientPermeationColor = AmbientPermeationColor,

            // Advanced
            Type = Type,
            RefreshRayCount = RefreshRayCount,
            RefreshDistanceThreshold = RefreshDistanceThreshold,
            ScatteringSeed = ScatteringSeed,
            ClampPosition = ClampPosition,
        };

        AddChild(emitter);
    }

    void OnDeviceRecreated()
    {
        // Re-play if we were playing before the device was destroyed
        if (_wasPlayingBeforeDeviceDestroyed)
        {
            _wasPlayingBeforeDeviceDestroyed = false;
            Play();
        }
    }

    void OnRaytracedByAnotherEmitter(vaudio.Emitter other)
    {
        ApplyRaytracingResults(other);

        if (PlayWhenRaytracingCompletes)
            Play();
    }

    void OnEmitterRemoved()
    {
        Debug.Assert(emitter != null);

        RemoveChild(emitter);
        emitter = null;
    }

    bool played = false;

    public override bool Play()
    {
        if (!Raytraced)
        {
            PlayWhenRaytracingCompletes = true;
            return false;
        }

        return played = base.Play();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Raytraced)
        {
            if (!played && PlayWhenRaytracingCompletes)
                Play();

            ApplyRaytracingResults(vercidiumAudio.listener.emitter);
        }
    }

    void ApplyRaytracingResults(vaudio.Emitter other)
    {
        effect = vercidiumAudio.GetReverbEffect(emitter);

        if (other.HasRaytracedTarget(emitter.emitter))
        {
            var vaudioFilter = other.GetTargetFilter(emitter.emitter);
            UpdateFilter(vaudioFilter.GainLF, vaudioFilter.GainHF, true);
        }
    }

    public override void OnDeviceDestroyed()
    {
        // Track if we were playing so we can re-play after device recreation
        _wasPlayingBeforeDeviceDestroyed = played && Looping;

        // Reset played state since sources are being destroyed
        played = false;

        base.OnDeviceDestroyed();
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint())
        {
            base._ExitTree();
            return;
        }

        // Unregister the device recreated callback (only registered when not in editor)
        ALManager.instance?.UnregisterDeviceRecreatedCallback(OnDeviceRecreated);

        base._ExitTree();
    }
}
