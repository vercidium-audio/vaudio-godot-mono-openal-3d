namespace vaudio_godot_mono_openal;

public partial class VAEmitter
{
    [ExportGroup("Reverb")]

    int _ReverbRayCount = 0;
    /// <summary>Number of reverb rays cast</summary>
    [Export]
    public int ReverbRayCount
    {
        get => _ReverbRayCount;
        set
        {
            _ReverbRayCount = Math.Max(0, value);

            if (emitter != null)
                emitter.ReverbRayCount = _ReverbRayCount;
        }
    }

    int _ReverbBounceCount = 0;
    /// <summary>Number of bounces per reverb ray</summary>
    [Export]
    public int ReverbBounceCount
    {
        get => _ReverbBounceCount;
        set
        {
            _ReverbBounceCount = Math.Max(0, value);

            if (emitter != null)
                emitter.ReverbBounceCount = _ReverbBounceCount;
        }
    }

    float _ReverbEnergyCap = 0.15f;
    /// <summary>
    /// The percentage of returning energy required for reverb to be at maximum volume. Defaults to 15% of this emitter's <see cref="ReverbRayCount"/> * <see cref="ReverbBounceCount"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is NaN, Infinity, less than or equal to 0, or greater than 1</exception>
    [Export(PropertyHint.Range, "0.001,1.0")]
    public float ReverbEnergyCap
    {
        get => _ReverbEnergyCap;
        set
        {
            _ReverbEnergyCap = value;

            if (emitter != null)
                emitter.ReverbEnergyCap = _ReverbEnergyCap;
        }
    }

    float _MaxVolume = 1.0f;
    /// <summary>
    /// The loudest linear volume (0–1) this emitter's dry source will ever be played at by the consuming application. Used to estimate how long the emitter's reverb tail stays audible - a quieter source reaches an inaudible reverb tail sooner. Defaults to 1 (full volume)
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is NaN, Infinity, less than 0 or greater than 1</exception>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float MaxVolume
    {
        get => _MaxVolume;
        set
        {
            _MaxVolume = value;

            if (emitter != null)
                emitter.MaxVolume = _MaxVolume;
        }
    }

    int _MaxEchogramTime = 5000;
    /// <summary>How long (in milliseconds) the echogram records data for. Returning reverb rays after this period will be ignored</summary>
    [Export]
    public int MaxEchogramTime
    {
        get => _MaxEchogramTime;
        set
        {
            _MaxEchogramTime = Math.Max(0, value);

            if (emitter != null)
            {
                emitter.MaxEchogramTime = _MaxEchogramTime;
            }
        }
    }

    int _EchogramGranularity = 100;
    /// <summary>The length (in milliseconds) of each entry in the echogram</summary>
    [Export]
    public int EchogramGranularity
    {
        get => _EchogramGranularity;
        set
        {
            _EchogramGranularity = Math.Max(0, value);

            if (emitter != null)
            {
                emitter.EchogramGranularity = _EchogramGranularity;
            }
        }
    }

    bool _AffectsGroupedEAX = true;
    /// <summary>
    /// Controls whether this Emitter's EAX is blended to produced grouped EAX. Set this to false for listener emitters.
    /// Mutually exclusive with HasRelativeReverb - both can't be true at the same time.
    /// </summary>
    [Export]
    public bool AffectsGroupedEAX
    {
        get => _AffectsGroupedEAX;
        set
        {
            _AffectsGroupedEAX = value;

            if (emitter != null)
                emitter.AffectsGroupedEAX = value;

            if (value)
                HasRelativeReverb = false;
        }
    }

    bool _HasRelativeReverb = true;
    /// <summary>
    /// Whether this emitter is used as a reference point for calculating relative reverb gain and direction.
    /// Mutually exclusive with AffectsGroupedEAX - both can't be true at the same time.
    /// </summary>
    [Export]
    public bool HasRelativeReverb
    {
        get => _HasRelativeReverb;
        set
        {
            _HasRelativeReverb = value;

            if (emitter != null)
                emitter.HasRelativeReverb = value;

            if (value)
                AffectsGroupedEAX = false;
        }
    }

    bool _UseListenerReverb = false;
    /// <summary>If true, this emitter's reverb send uses the listener's reverb effect rather than its own</summary>
    [Export]
    public bool UseListenerReverb
    {
        get => _UseListenerReverb;
        set => _UseListenerReverb = value;
    }

    float _RelativeReverbInnerThreshold = 0.6f;
    /// <summary>The lower bound of the relative reverb blend range</summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float RelativeReverbInnerThreshold
    {
        get => _RelativeReverbInnerThreshold;
        set
        {
            _RelativeReverbInnerThreshold = value;

            if (emitter != null)
                emitter.RelativeReverbInnerThreshold = value;
        }
    }

    float _RelativeReverbOuterThreshold = 0.8f;
    /// <summary>The upper bound of the relative reverb blend range</summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float RelativeReverbOuterThreshold
    {
        get => _RelativeReverbOuterThreshold;
        set
        {
            _RelativeReverbOuterThreshold = value;

            if (emitter != null)
                emitter.RelativeReverbOuterThreshold = value;
        }
    }


    [ExportGroup("Muffling")]

    int _OcclusionRayCount = 0;
    /// <summary>Number of occlusion rays cast</summary>
    [Export]
    public int OcclusionRayCount
    { 
        get => _OcclusionRayCount;
        set
        {
            _OcclusionRayCount = Math.Max(0, value);

            if (emitter != null)
                emitter.OcclusionRayCount = _OcclusionRayCount;
        }
    }
    
    int _OcclusionBounceCount = 0;
    /// <summary>Number of bounces per occlusion ray</summary>
    [Export]
    public int OcclusionBounceCount
    { 
        get => _OcclusionBounceCount;
        set
        {
            _OcclusionBounceCount = Math.Max(0, value);

            if (emitter != null)
                emitter.OcclusionBounceCount = _OcclusionBounceCount;
        }
    }
    
    float _OcclusionEnergyCap = 0.15f;
    /// <summary>
    /// The percentage of occlusion energy required for this emitter to be at full volume. Defaults to 15% of the other emitter's <see cref="OcclusionRayCount"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is NaN, Infinity, or less than 0</exception>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float OcclusionEnergyCap
    { 
        get => _OcclusionEnergyCap;
        set
        {
            _OcclusionEnergyCap = Math.Max(0, value);

            if (emitter != null)
                emitter.OcclusionEnergyCap = _OcclusionEnergyCap;
        }
    }

    int _PermeationRayCount = 0;
    /// <summary>Number of permeation rays cast</summary>
    [Export]
    public int PermeationRayCount
    { 
        get => _PermeationRayCount;
        set
        {
            _PermeationRayCount = Math.Max(0, value);

            if (emitter != null)
                emitter.PermeationRayCount = _PermeationRayCount;
        }
    }
    
    int _PermeationBounceCount = 0;
    /// <summary>Number of bounces per permeation ray</summary>
    [Export]
    public int PermeationBounceCount
    { 
        get => _PermeationBounceCount;
        set
        {
            _PermeationBounceCount = Math.Max(0, value);

            if (emitter != null)
                emitter.PermeationBounceCount = _PermeationBounceCount;
        }
    }

    float _PermeationEnergyCap = 0.15f;
    /// <summary>
    /// The percentage of permeation energy required for this emitter to be at full volume. Defaults to 15% of the other emitter's <see cref="PermeationRayCount"/> * <see cref="PermeationBounceCount"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is NaN, Infinity, or less than 0</exception>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float PermeationEnergyCap
    {
        get => _PermeationEnergyCap;
        set
        {
            _PermeationEnergyCap = Math.Max(0, value);

            if (emitter != null)
                emitter.PermeationEnergyCap = _PermeationEnergyCap;
        }
    }


    [ExportGroup("Ambience")]

    int _AmbientOcclusionRayCount = 0;
    /// <summary>Number of ambient occlusion rays cast</summary>
    [Export]
    public int AmbientOcclusionRayCount
    { 
        get => _AmbientOcclusionRayCount;
        set
        {
            _AmbientOcclusionRayCount = Math.Max(0, value);

            if (emitter != null)
                emitter.AmbientOcclusionRayCount = _AmbientOcclusionRayCount;
        }
    }
    
    int _AmbientOcclusionBounceCount = 0;
    /// <summary>Number of bounces per ambient occlusion ray</summary>
    [Export]
    public int AmbientOcclusionBounceCount
    { 
        get => _AmbientOcclusionBounceCount;
        set
        {
            _AmbientOcclusionBounceCount = Math.Max(0, value);

            if (emitter != null)
                emitter.AmbientOcclusionBounceCount = _AmbientOcclusionBounceCount;
        }
    }

    float _AmbientOcclusionEnergyCap = 0.15f;
    /// <summary>
    /// The percentage of occlusion energy required for the emitter to be at full volume. Defaults to 15% of this emitter's <see cref="AmbientOcclusionRayCount"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is NaN, Infinity, or less than 0</exception>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float AmbientOcclusionEnergyCap
    {
        get => _AmbientOcclusionEnergyCap;
        set
        {
            _AmbientOcclusionEnergyCap = Math.Max(0, value);

            if (emitter != null)
                emitter.AmbientOcclusionEnergyCap = _AmbientOcclusionEnergyCap;
        }
    }

    int _AmbientPermeationRayCount = 0;
    /// <summary>Number of ambient permeation rays cast</summary>
    [Export]
    public int AmbientPermeationRayCount
    { 
        get => _AmbientPermeationRayCount;
        set
        {
            _AmbientPermeationRayCount = Math.Max(0, value);

            if (emitter != null)
                emitter.AmbientPermeationRayCount = _AmbientPermeationRayCount;
        }
    }
    
    int _AmbientPermeationBounceCount = 0;
    /// <summary>Number of bounces per ambient permeation ray</summary>
    [Export]
    public int AmbientPermeationBounceCount
    { 
        get => _AmbientPermeationBounceCount;
        set
        {
            _AmbientPermeationBounceCount = Math.Max(0, value);

            if (emitter != null)
                emitter.AmbientPermeationBounceCount = _AmbientPermeationBounceCount;
        }
    }

    float _AmbientPermeationEnergyCap = 0.15f;
    /// <summary>
    /// The percentage of permeation energy required for the emitter to be at full volume. Defaults to 15% of this emitter's <see cref="AmbientPermeationRayCount"/> * <see cref="AmbientPermeationBounceCount"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is NaN, Infinity, or less than 0</exception>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float AmbientPermeationEnergyCap
    {
        get => _AmbientPermeationEnergyCap;
        set
        {
            _AmbientPermeationEnergyCap = Math.Max(0, value);

            if (emitter != null)
                emitter.AmbientPermeationEnergyCap = _AmbientPermeationEnergyCap;
        }
    }

    [ExportGroup("Debug Rendering")]

    bool _RandomTrailColor = false;
    /// <summary>If true, renders each trail a different color in the debug window (dev build only)</summary>
    [Export]
    public bool RandomTrailColor
    {
        get => _RandomTrailColor;
        set
        {
            _RandomTrailColor = value;

            if (emitter != null)
                emitter.RandomTrailColor = value;
        }
    }

    Godot.Color _TrailColor = new(1.0f, 1.0f, 1.0f, 0.1f);
    /// <summary>
    /// The color of ray trails in the debug window (dev build only)
    /// </summary>
    [Export]
    public Godot.Color TrailColor
    {
        get => _TrailColor;
        set
        {
            _TrailColor = value;

            if (emitter != null)
                emitter.TrailColor = ToVAudio(value);
        }
    }

    Godot.Color _ReverbColor = new(0.11f, 0.97f, 1.0f, 0.2f);
    /// <summary>
    /// The color of reverb rays in the debug window (dev build only)
    /// </summary>
    [Export]
    public Godot.Color ReverbColor
    {
        get => _ReverbColor;
        set
        {
            _ReverbColor = value;

            if (emitter != null)
                emitter.ReverbColor = ToVAudio(value);
        }
    }

    Godot.Color _OcclusionColor = new(0.44f, 1.0f, 0.64f, 0.2f);
    /// <summary>
    /// The color of occlusion rays in the debug window (dev build only)
    /// </summary>
    [Export]
    public Godot.Color OcclusionColor
    {
        get => _OcclusionColor;
        set
        {
            _OcclusionColor = value;

            if (emitter != null)
                emitter.OcclusionColor = ToVAudio(value);
        }
    }

    Godot.Color _PermeationColor = new(1.0f, 0.5f, 0.17f, 0.2f);
    /// <summary>
    /// The color of permeation rays in the debug window (dev build only)
    /// </summary>
    [Export]
    public Godot.Color PermeationColor
    {
        get => _PermeationColor;
        set
        {
            _PermeationColor = value;

            if (emitter != null)
                emitter.PermeationColor = ToVAudio(value);
        }
    }

    Godot.Color _AmbientPermeationColor = new(1.0f, 0.8f, 0.0f, 0.2f);
    /// <summary>
    /// The color of ambientPermeation rays in the debug window (dev build only)
    /// </summary>
    [Export]
    public Godot.Color AmbientPermeationColor
    {
        get => _AmbientPermeationColor;
        set
        {
            _AmbientPermeationColor = value;

            if (emitter != null)
                emitter.AmbientPermeationColor = ToVAudio(value);
        }
    }

    [ExportGroup("Advanced")]

    int _Type;
    /// <summary>User-defined type for this emitter</summary>
    [Export]
    public int Type
    {
        get => _Type;
        set
        {
            _Type = value;

            if (emitter != null)
                emitter.Type = value;
        }
    }

    int _RefreshRayCount = 16;
    /// <summary>
    /// The number of trails that are rebuilt from scratch each frame to prevent staleness when the listener moves. Clamped to minimum of 0.
    /// </summary>
    [Export]
    public int RefreshRayCount
    {
        get => _RefreshRayCount;
        set
        {
            _RefreshRayCount = value;

            if (emitter != null)
                emitter.RefreshRayCount = value;
        }
    }

    float _RefreshDistanceThreshold = 1.0f;
    /// <summary>
    /// A ray trail will be re-created if an old ray bounce position is too far away from the new ray bounce position. This setting controls the allowed distance between old and new ray bounce positions. Defaults to 1.0f. Clamped to minimum of 0.
    /// </summary>
    [Export]
    public float RefreshDistanceThreshold
    {
        get => _RefreshDistanceThreshold;
        set
        {
            _RefreshDistanceThreshold = value;

            if (emitter != null)
                emitter.RefreshDistanceThreshold = value;
        }
    }

    int _ScatteringSeed = Random.Shared.Next(int.MaxValue);
    /// <summary>A seed used to randomise scattering vectors</summary>
    [Export]
    public int ScatteringSeed
    {
        get => _ScatteringSeed;
        set
        {
            _ScatteringSeed = value;

            if (emitter != null)
                emitter.ScatteringSeed = value;
        }
    }

    bool _ClampPosition = true;
    /// <summary>Whether to clamp this emitter's position to the world bounds, to prevent it from going out of bounds</summary>
    [Export]
    public bool ClampPosition
    {
        get => _ClampPosition;
        set
        {
            _ClampPosition = value;

            if (emitter != null)
                emitter.ClampPosition = value;
        }
    }
}
