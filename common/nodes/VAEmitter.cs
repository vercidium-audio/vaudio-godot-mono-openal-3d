namespace vaudio_godot_mono_openal;

// Base type (Node2D/Node3D) and [Tool]/[GlobalClass] are declared per-addon in VAEmitterBase.cs.
public partial class VAEmitter
{
    public bool IsMainListener => this is VAListener;

    VAWorld vercidiumAudio;
    public vaudio.Emitter emitter;

    public ALReverbEffect effect;
    public ALFilter filter;

    public float GainLF => filter?.gain ?? 0;
    public float GainHF => filter?.gainHF ?? 0;
    public bool Raytraced => emitter != null && !emitter.Initialising;

    // Set while waiting for a VAWorld to appear. _ExitTree cancels the pending retry if this node leaves the tree
    Action cancelWaitForVAWorld;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
            return;

        ALManager.Ensure();

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
        emitter.OnRemoved = OnEmitterRemoved;
    }

    public void RemoveEmitter()
    {
        if (emitter == null)
            throw new InvalidOperationException("Emitter already removed");

        // Don't null out emitter here, need to wait for its pending reverb tail to finish
        vercidiumAudio.RemoveEmitter(emitter);
    }

    void OnEmitterRemoved()
    {
        emitter = null;
        OnEmitterRemovedCallback?.Invoke();
    }

    void OnRaytracingComplete()
    {
        OnRaytracingCompleteCallback?.Invoke();
    }

    void OnRaytracedByAnotherEmitter(vaudio.Emitter emitter)
    {
        ALManager.Ensure();

        Debug.Assert(filter == null);
        filter = new(1, 1);

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

        // If no listener, we can't know how muffled we are
        if (vercidiumAudio.listener == null)
            return;

        // Apply filter if we aren't the listener
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
        if (cancelWaitForVAWorld != null)
        {
            cancelWaitForVAWorld();
            cancelWaitForVAWorld = null;

            LogWarning($"'{Name}' left the tree without ever finding a VAWorld - no emitter was created for it. Make sure this node's scene was added under a VAWorld while it was in the tree.");
        }

        if (emitter != null)
        {
            vercidiumAudio.UnregisterPendingTarget(emitter);
            vercidiumAudio.UnregisterListener(this);

            RemoveEmitter();
        }

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

    public bool IsAmbientFilterReady => Raytraced && AmbientFilter != null;
    public float GetAmbientFilterGainLF() => AmbientFilter?.GainLF ?? 1.0f;
    public float GetAmbientFilterGainHF() => AmbientFilter?.GainHF ?? 1.0f;

    public Action OnRaytracingCompleteCallback;
    public Action<vaudio.Emitter> OnRaytracedByAnotherEmitterCallback;
    public Action OnEmitterRemovedCallback;
}
