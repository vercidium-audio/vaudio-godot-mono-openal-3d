namespace vaudio_godot_mono_openal;

/// <summary>
/// Renders an emitter's <see cref="vaudio.Emitter.VisualisationCallback"/> ray-bounce results as
/// fading diamond sprites. Add it as a direct child of a <see cref="VAEmitter"/>, or of a
/// <see cref="VARaytracedSource"/> such as <see cref="VASource"/>/<see cref="VAStreamSource"/> - in
/// the source case it binds to that source's internal emitter (VARaytracedSource.RaytracedEmitter)
/// without being moved in the tree. This node registers itself as the visualisation callback
/// target and pushes the exported RayCount/BounceCount/UpdateFrequencyMs onto that emitter. Each
/// callback invocation (every UpdateFrequencyMs) writes one MultiMesh instance per
/// VisualisationData entry; per-frame fading is entirely GPU-side (a ShaderMaterial reads each
/// instance's spawn time out of its custom data and the FadeInMs/FadeOutMs/DurationMs/Color
/// uniforms below), so nothing here does per-instance work outside the callback itself.
///
/// Because this node stays at the scene path the user authored it at (it is never reparented),
/// the editor's Debug > Sync Scene Changes forwards runtime property edits to the running
/// instance - so RayCount, Color, FadeInMs etc. can all be tweaked live while the game runs, the
/// same way an emitter's own ray counts can.
///
/// Adding this node while the game is already running only runs its script in the editor's own
/// tool-script preview context (Engine.IsEditorHint() is true there) - Godot Mono does not
/// actually instantiate the new C# node inside the live running game process the way GDScript
/// nodes do (upstream Godot C# hot-reload limitation - godotengine/godot-proposals#7746/#9001/
/// #9519). Restart the game to see a newly-added VAVisualisation render.
/// </summary>
[Tool]
[GlobalClass]
public partial class VAVisualisation : Node3D
{
    const string ShaderCode = """
        shader_type spatial;
        render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

        uniform float current_time;
        uniform float fade_in_ms;
        uniform float fade_out_ms;
        uniform float duration_ms;
        uniform vec4 base_color : source_color;

        varying float spawn_time;

        void vertex() {
            spawn_time = INSTANCE_CUSTOM.x;
        }

        void fragment() {
            float elapsed_ms = (current_time - spawn_time) * 1000.0;
            if (elapsed_ms < 0.0 || elapsed_ms > duration_ms) discard;

            float fade_in = fade_in_ms > 0.0 ? clamp(elapsed_ms / fade_in_ms, 0.0, 1.0) : 1.0;
            float fade_out = fade_out_ms > 0.0 ? clamp((duration_ms - elapsed_ms) / fade_out_ms, 0.0, 1.0) : 1.0;

            ALBEDO = base_color.rgb;
            ALPHA = base_color.a * min(fade_in, fade_out);
        }
        """;

    // The VAEmitter node whose vaudio.Emitter this node drives + renders. Either this node's direct
    // VAEmitter parent, or the internal emitter of a VARaytracedSource parent (VASource etc.).
    VAEmitter emitter;

    MultiMesh multimesh;
    MultiMeshInstance3D multimeshInstance;
    ShaderMaterial shaderMaterial;

    // Ring-buffer write cursor into multimesh's instances - each callback invocation appends
    // RayCount * BounceCount new instances here, wrapping once the buffer is full. The buffer is
    // sized (see RequiredInstanceCount) to hold every batch still fading at once, so by the time
    // the cursor wraps back around to overwrite an instance, that instance has already faded out -
    // no bookkeeping needed to free instances early.
    int nextInstance = 0;

    bool waitingForParent = false;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
            return;

        FindEmitter();

        if (emitter == null)
        {
            // The parent VAEmitter/VARaytracedSource may not have created its vaudio.Emitter yet
            // (VARaytracedSource creates it only once a VAWorld is found) - retry as nodes appear.
            waitingForParent = true;
            GetTree().NodeAdded += RetryFindEmitter;
        }
    }

    public override void _ExitTree()
    {
        if (waitingForParent)
        {
            GetTree().NodeAdded -= RetryFindEmitter;
            waitingForParent = false;
        }

        if (emitter?.emitter != null)
            emitter.emitter.VisualisationCallback = null;

        emitter = null;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
            return;

        CreateMultimesh();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        shaderMaterial?.SetShaderParameter("current_time", Time.GetTicksMsec() / 1000.0);
    }

    public override string[] _GetConfigurationWarnings()
    {
        if (GetParent() is not VAEmitter and not VARaytracedSource)
            return ["This node must be a direct child of a VAEmitter (or a VARaytracedSource such as VASource/VAStreamSource) to render its visualisation rays."];

        return [];
    }

    void FindEmitter()
    {
        // Accept either a VAEmitter parent directly, or a VARaytracedSource parent (VASource /
        // VAStreamSource) and use the emitter it creates internally. Either way this node is not
        // moved in the tree.
        var candidate = GetParent() switch
        {
            VAEmitter parentEmitter => parentEmitter,
            VARaytracedSource parentSource => parentSource.RaytracedEmitter,
            _ => null,
        };

        if (candidate?.emitter == null)
        {
            // Parent isn't a VAEmitter/VARaytracedSource, or it hasn't created its vaudio.Emitter
            // yet - either way, retry later via NodeAdded.
            emitter = null;
            return;
        }

        emitter = candidate;

        ApplyPropertiesToEmitter();

        emitter.emitter.VisualisationCallback = OnVisualisationData;
    }

    void RetryFindEmitter(Node node)
    {
        FindEmitter();

        if (emitter == null)
        {
            if (GetParent() is not VAEmitter and not VARaytracedSource)
                LogWarning($"[vaudio-godot-mono-openal-3d] {Name} must be a direct child of a VAEmitter, VASource or VAStreamSource to render its visualisation rays.");

            return;
        }

        GetTree().NodeAdded -= RetryFindEmitter;
        waitingForParent = false;
    }

    void ApplyPropertiesToEmitter()
    {
        emitter.emitter.VisualisationRayCount = _RayCount;
        emitter.emitter.VisualisationBounceCount = _BounceCount;
        emitter.emitter.VisualisationUpdateFrequency = _UpdateFrequencyMs;
    }

    // Diamonds must stay alive (fading) for DurationMs while new callbacks arrive every
    // UpdateFrequencyMs, so the ring buffer needs room for every batch still fading at once - not
    // just the latest one - or a new callback's writes stomp on still-visible instances from a
    // few callbacks ago.
    int RequiredInstanceCount()
    {
        int batchSize = Math.Max(1, _RayCount * _BounceCount);
        int batchesInFlight = (_DurationMs / Math.Max(1, _UpdateFrequencyMs)) + 2;

        return batchSize * batchesInFlight;
    }

    static ArrayMesh BuildDiamondMesh()
    {
        // Unit diamond in the local XY plane, facing +Z - orientated per-instance to the ray hit
        // normal via each MultiMesh instance's transform (see OnVisualisationData).
        Vector3 top = new(0, 1, 0);
        Vector3 right = new(1, 0, 0);
        Vector3 bottom = new(0, -1, 0);
        Vector3 left = new(-1, 0, 0);

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        st.AddVertex(top);
        st.AddVertex(right);
        st.AddVertex(bottom);

        st.AddVertex(top);
        st.AddVertex(bottom);
        st.AddVertex(left);

        return st.Commit();
    }

    void CreateMultimesh()
    {
        var shader = new Shader { Code = ShaderCode };

        shaderMaterial = new ShaderMaterial { Shader = shader };
        ApplyShaderUniforms();

        multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = BuildDiamondMesh(),
            InstanceCount = RequiredInstanceCount(),
            VisibleInstanceCount = 0,
        };

        multimeshInstance = new MultiMeshInstance3D
        {
            Multimesh = multimesh,
            MaterialOverride = shaderMaterial,

            // VisualisationData positions/normals are already in world space - TopLevel makes this
            // node's own transform (left at identity) the effective global transform, so the
            // world-space positions written in OnVisualisationData can be used directly instead of
            // being re-relativised to a moving parent every time the emitter moves.
            TopLevel = true,
            Transform = Transform3D.Identity,

            // Instance transforms are written from the visualisation callback on a regular
            // _Process frame, not the physics step - physics interpolation has nothing valid to
            // interpolate between and just warns on every write.
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,

            // Rays can land anywhere within the VAWorld, far outside this node's own transform - a
            // per-instance frustum test against this node's local AABB would cull them incorrectly.
            IgnoreOcclusionCulling = true,
            ExtraCullMargin = 1024.0f,
        };

        AddChild(multimeshInstance);
    }

    void ApplyShaderUniforms()
    {
        if (shaderMaterial == null)
            return;

        shaderMaterial.SetShaderParameter("fade_in_ms", (float)_FadeInMs);
        shaderMaterial.SetShaderParameter("fade_out_ms", (float)_FadeOutMs);
        shaderMaterial.SetShaderParameter("duration_ms", (float)_DurationMs);
        shaderMaterial.SetShaderParameter("base_color", _Color);
    }

    // Called from vaudio's main-thread update - writes one MultiMesh instance per ray bounce,
    // wrapping the ring-buffer cursor rather than doing any per-instance cleanup - a stale
    // instance from several callbacks ago simply keeps rendering until this cursor wraps back
    // around to overwrite it, by which point the shader has long since faded it to zero alpha.
    void OnVisualisationData(vaudio.VisualisationData[] data)
    {
        if (multimesh == null || data.Length == 0)
            return;

        int capacity = multimesh.InstanceCount;
        int needed = Math.Max(data.Length, RequiredInstanceCount());

        if (needed > capacity)
        {
            capacity = needed;
            multimesh.InstanceCount = capacity;
            nextInstance = 0;
        }

        double now = Time.GetTicksMsec() / 1000.0;
        Vector3 upHint = new(0, 1, 0);
        Vector3 emitterPosition = emitter?.GlobalPosition ?? Vector3.Zero;
        float maxDistanceSquared = _MaxDistance * _MaxDistance;

        for (int i = 0; i < data.Length; i++)
        {
            Vector3 position = FromVAudio(data[i].position);

            // Skip bounces too far from the emitter to be worth drawing, rather than writing them
            // and hiding them in the shader - keeps the ring buffer's slots for bounces actually
            // worth rendering.
            if (_MaxDistance > 0.0f && position.DistanceSquaredTo(emitterPosition) > maxDistanceSquared)
                continue;

            Vector3 normal = FromVAudio(data[i].normal);
            normal = normal.LengthSquared() > 0.00001f ? normal.Normalized() : new Vector3(0, 1, 0);

            // Avoid a degenerate basis when the normal is parallel to the up hint.
            Vector3 basisUp = Math.Abs(normal.Dot(upHint)) > 0.999f ? Vector3.Right : upHint;

            Basis basis = Basis.LookingAt(normal, basisUp).Scaled(new Vector3(_Size, _Size, _Size));

            // Nudge each diamond off the surface it landed on, along the hit normal, to avoid
            // z-fighting with the world geometry it's rendered flush against.
            Transform3D transform = new(basis, position + normal * _NormalOffset);

            multimesh.SetInstanceTransform(nextInstance, transform);
            multimesh.SetInstanceCustomData(nextInstance, new Color((float)now, 0, 0, 0));

            nextInstance = (nextInstance + 1) % capacity;
        }

        multimesh.VisibleInstanceCount = capacity;
    }

    [ExportGroup("Ray Casting")]

    int _RayCount = 32;
    /// <summary>Number of visualisation rays cast</summary>
    [Export]
    public int RayCount
    {
        get => _RayCount;
        set
        {
            _RayCount = Math.Max(0, value);

            if (emitter?.emitter != null)
                emitter.emitter.VisualisationRayCount = _RayCount;
        }
    }

    int _BounceCount = 4;
    /// <summary>Number of times each visualisation ray bounces</summary>
    [Export]
    public int BounceCount
    {
        get => _BounceCount;
        set
        {
            _BounceCount = Math.Max(0, value);

            if (emitter?.emitter != null)
                emitter.emitter.VisualisationBounceCount = _BounceCount;
        }
    }

    int _UpdateFrequencyMs = 500;
    /// <summary>How often - in milliseconds - to cast visualisation rays</summary>
    [Export]
    public int UpdateFrequencyMs
    {
        get => _UpdateFrequencyMs;
        set
        {
            _UpdateFrequencyMs = Math.Max(1, value);

            if (emitter?.emitter != null)
                emitter.emitter.VisualisationUpdateFrequency = _UpdateFrequencyMs;
        }
    }

    [ExportGroup("Appearance")]

    int _FadeInMs = 100;
    /// <summary>How long - in milliseconds - each diamond takes to fade in</summary>
    [Export]
    public int FadeInMs
    {
        get => _FadeInMs;
        set
        {
            _FadeInMs = Math.Max(0, value);
            ApplyShaderUniforms();
        }
    }

    int _FadeOutMs = 400;
    /// <summary>How long - in milliseconds - each diamond takes to fade out</summary>
    [Export]
    public int FadeOutMs
    {
        get => _FadeOutMs;
        set
        {
            _FadeOutMs = Math.Max(0, value);
            ApplyShaderUniforms();
        }
    }

    int _DurationMs = 1500;
    /// <summary>Total lifetime - in milliseconds - of each diamond, including fade in/out</summary>
    [Export]
    public int DurationMs
    {
        get => _DurationMs;
        set
        {
            _DurationMs = Math.Max(0, value);
            ApplyShaderUniforms();
        }
    }

    Color _Color = new(0.11f, 0.97f, 1.0f, 0.75f);
    /// <summary>RGB is the diamond colour, A is the maximum opacity once faded in</summary>
    [Export]
    public Color Color
    {
        get => _Color;
        set
        {
            _Color = value;
            ApplyShaderUniforms();
        }
    }

    float _Size = 0.1f;
    /// <summary>Size of each diamond. Only affects future instance writes, not retroactive</summary>
    [Export(PropertyHint.Range, "0.01,5.0,0.01,or_greater")]
    public float Size
    {
        get => _Size;
        set => _Size = Math.Max(0.01f, value);
    }

    float _NormalOffset = 0.02f;
    /// <summary>World units each diamond is pushed along its hit normal, to avoid z-fighting with the surface it landed on</summary>
    [Export(PropertyHint.Range, "0.0,1.0,0.001,or_greater")]
    public float NormalOffset
    {
        get => _NormalOffset;
        set => _NormalOffset = Math.Max(0.0f, value);
    }

    float _MaxDistance = 20.0f;
    /// <summary>Maximum distance from the parent emitter a ray bounce can be to be rendered. 0 = unlimited</summary>
    [Export(PropertyHint.Range, "0.0,500.0,0.1,or_greater")]
    public float MaxDistance
    {
        get => _MaxDistance;
        set => _MaxDistance = Math.Max(0.0f, value);
    }
}
