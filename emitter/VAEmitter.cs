using System.Linq;

namespace vaudio_godot_openal;

[Tool]
[GlobalClass]
public partial class VAEmitter : Node3D
{
    VAWorld vercidiumAudio;
    public vaudio.Emitter emitter;

    public ALReverbEffect effect;
    public ALFilter filter;

    public float GainLF => filter?.gain ?? 0;
    public float GainHF => filter?.gainHF ?? 0;
    public bool Raytraced => emitter != null && !emitter.Initialising;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
            return;

        vercidiumAudio = this.GetVAWorldParent();

        if (vercidiumAudio == null)
        {
            GD.PushWarning($"[vaudio-godot-openal] Failed to initialise node {Name} because there is no VAWorld node. Ensure a VAWorld node exists higher up the tree");
            return;
        }

        if (!vercidiumAudio.Initialised)
        {
            vercidiumAudio.LogWarning($"Failed to initialise node {Name} because the VAWorld node is not initialised yet. Ensure the VAWorld node is higher up the tree");
            return;
        }

        CreateEmitter();
    }

    public override string[] _GetConfigurationWarnings()
    {
        var sceneRoot = Engine.IsEditorHint() ? GetTree()?.EditedSceneRoot : GetTree()?.CurrentScene;
        if (sceneRoot == null)
            return [];

        var vercidiumAudioNode = sceneRoot.GetChildren().OfType<VAWorld>().FirstOrDefault();
        if (vercidiumAudioNode == null)
            return ["No VAWorld node found. Ensure a VAWorld node exists higher up the tree."];

        // Find the top-level ancestor of this node (direct child of scene root)
        var ancestor = this as Node;
        while (ancestor.GetParent() != sceneRoot)
            ancestor = ancestor.GetParent();

        if (ancestor.GetIndex() < vercidiumAudioNode.GetIndex())
            return ["This node must be lower in the scene tree than the VAWorld node."];

        return [];
    }

    public void CreateEmitter()
    {
        if (emitter != null)
            throw new InvalidOperationException("Emitter already created");

        emitter = vercidiumAudio.CreateEmitter(this, OnRaytracingComplete, OnRaytracedByAnotherEmitter);
        emitter.OnRemoved = OnEmitterRemoved;
    }

    public void RemoveEmitter()
    {
        if (emitter == null)
            throw new InvalidOperationException("Emitter already removed");

        // Don't null out `emitter` here: if AffectsEAXAfterRemoval is set, the underlying vaudio.Emitter
        // stays alive (pendingRemoval) for its reverb tail and keeps polling Position — this node (and
        // anything it's attached to, e.g. VASource) must stay in the tree until OnEmitterRemoved fires.
        vercidiumAudio.RemoveEmitter(emitter);
    }

    void OnEmitterRemoved()
    {
        emitter = null;
        OnEmitterRemovedCallback?.Invoke();
    }

    void OnRaytracingComplete()
    {
        // Our own reverb and ambient permeation rays have been cast
        OnRaytracingCompleteCallback?.Invoke();
    }

    void OnRaytracedByAnotherEmitter(vaudio.Emitter emitter)
    {        
        if (GodotOpenALEnabled)
        {
            Debug.Assert(filter == null);
            filter = new(1, 1);
        }

        ApplyRaytracingResults();

        OnRaytracedByAnotherEmitterCallback?.Invoke(emitter);

        if (RaytraceOnce)
            RemoveEmitter();
    }

    public override void _Process(double delta)
    {
        // If initialisation failed, skip
        if (emitter == null)
            return;

        if (Raytraced)
            ApplyRaytracingResults();
    }

    void ApplyRaytracingResults()
    {
        effect = vercidiumAudio.GetReverbEffect(this);

        if (vercidiumAudio.listener == null)
            return;

        if (this != vercidiumAudio.listener)
        {
            if (vercidiumAudio.listener.HasRaytracedTarget(this))
            {
                var vaudioFilter = vercidiumAudio.listener.GetTargetFilter(this);
                filter?.SetGain(vaudioFilter.GainLF, vaudioFilter.GainHF);
            }
        }
    }

    public bool HasRaytracedTarget(VAEmitter target) => emitter.HasRaytracedTarget(target.emitter);
    public vaudio.LowPassFilter GetTargetFilter(VAEmitter target) => emitter.GetTargetFilter(target.emitter);
    public vaudio.LowPassFilter GetTargetFilter(vaudio.Emitter target) => emitter.GetTargetFilter(target);

    public override void _ExitTree()
    {
        // Remove emitter from the raytracing scene
        if (emitter != null)
            RemoveEmitter();

        base._ExitTree();
    }

    public void AddTarget(vaudio.Emitter target)
    {
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
    public int GroupedEAXIndex => emitter.GroupedEAXIndex;

    public Action OnRaytracingCompleteCallback;
    public Action<vaudio.Emitter> OnRaytracedByAnotherEmitterCallback;
    public Action OnEmitterRemovedCallback;

    // Top-level properties
    bool _IsMainListener;
    [Export]
    public bool IsMainListener
    {
        get => _IsMainListener;
        set
        {
            _IsMainListener = value;
        }
    }

    bool _RaytraceOnce;
    [Export]
    public bool RaytraceOnce
    {
        get => _RaytraceOnce;
        set
        {
            _RaytraceOnce = value;
        }
    }

    [ExportGroup("Orientation")]

    float _Pitch;
    [Export]
    public float Pitch
    {
        get => _Pitch;
        set
        {
            _Pitch = value;
        }
    }

    float _Yaw;
    [Export]
    public float Yaw
    {
        get => _Yaw;
        set
        {
            _Yaw = value;
        }
    }


}
