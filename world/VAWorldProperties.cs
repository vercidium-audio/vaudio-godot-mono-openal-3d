using Godot.Collections;
using OpenAL;

namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public Node3D SceneRoot;

    static readonly string[] HiddenTransformProperties =
    [
        "rotation", "rotation_degrees", "quaternion", "basis", "scale", "transform",
        "rotation_edit_mode", "rotation_order",
    ];

    public override void _ValidateProperty(Dictionary property)
    {
        string name = property["name"].AsStringName();

        if (System.Array.IndexOf(HiddenTransformProperties, name.ToString()) != -1)
        {
            var usage = property["usage"].As<PropertyUsageFlags>();
            usage &= ~PropertyUsageFlags.Editor;
            property["usage"] = (int)usage;
        }
    }

    [ExportGroup("World")]

    string _WorldName = string.Empty;
    /// <summary>
    /// The name displayed in the debug window and log messages. Uses the node's name when empty
    /// </summary>
    [Export]
    public string WorldName
    {
        get => _WorldName;
        set
        {
            _WorldName = value ?? string.Empty;

            if (world != null)
                world.Name = ResolvedWorldName;
        }
    }

    string ResolvedWorldName => string.IsNullOrEmpty(_WorldName) ? Name.ToString() : _WorldName;

    Vector3 _Size = new(200, 100, 200);
    /// <summary>
    /// The size of the world.<br/>
    /// <see cref="VASource"/>s outside the world will not be raytraced, and Nodes that are fully outside these bounds will be ignored
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if NaN, infinity or &lt;= 0</exception>
    [Export]
    public Vector3 Size
    {
        get => _Size;
        set
        {
            _Size = ClampBoundsSize(value);
            UpdateGizmos();

            if (world == null)
                return;

            world.Size = ToVAudio(_Size);

            // Stay centred on the listener, where a fixed box keeps its minimum corner
            if (_BoundsFollow == VABoundsFollow.Listener)
                PlaceBounds();
        }
    }

    VABoundsFollow _BoundsFollow = VABoundsFollow.None;
    /// <summary>
    /// What the bounds follow during play.<br/>
    /// With <see cref="VABoundsFollow.Listener"/>, set <see cref="Size"/> to cover the area that's loaded around the player, and this node's position only places the box in the editor and until a listener enters the tree
    /// </summary>
    [Export]
    public VABoundsFollow BoundsFollow
    {
        get => _BoundsFollow;
        set
        {
            _BoundsFollow = value;
            ReclampBoundsSize();

            if (world != null)
                PlaceBounds();
        }
    }

    float _BoundsUpdateDistance = 25.0f;
    /// <summary>
    /// How far the current listener must move during play, in world units, before the bounds follow it, with <see cref="BoundsFollow"/> set to <see cref="VABoundsFollow.Listener"/>.<br/>
    /// Every move of the bounds re-checks all raytraced trails, so they should move in steps. While following the listener, <see cref="Size"/> is clamped to at least twice this value on every axis, so the listener can't walk out of the bounds before they recentre. 0 follows every move
    /// </summary>
    [Export(PropertyHint.Range, "0,1000,0.1,or_greater")]
    public float BoundsUpdateDistance
    {
        get => _BoundsUpdateDistance;
        set
        {
            _BoundsUpdateDistance = Mathf.Max(0.0f, value);
            ReclampBoundsSize();
        }
    }

    // The position last pushed to the SDK, and the listener it was centred on
    Vector3 boundsPosition;
    VAEmitter boundsListener;

    /// <summary>
    /// The bounds' minimum corner in the SDK during play, which differs from this node's position while <see cref="BoundsFollow"/> is <see cref="VABoundsFollow.Listener"/>. Returns this node's position in the editor
    /// </summary>
    public Vector3 GetBoundsPosition() => world != null ? FromVAudio(world.Position) : Position;

    /// <summary>
    /// Whether a position is within the bounds, edges included like the SDK's own test. See <see cref="VAEmitter.IsOutsideBounds"/> for the clamp and <see cref="EmittersOutsideTheWorldAreMuffled"/> rules
    /// </summary>
    public bool IsWithinBounds(Vector3 position)
    {
        var min = GetBoundsPosition();
        var max = min + _Size;
        return position.X >= min.X && position.Y >= min.Y && position.Z >= min.Z && position.X <= max.X && position.Y <= max.Y && position.Z <= max.Z;
    }

    // Where BoundsFollow wants the bounds' minimum corner. Following a listener while there is none leaves the bounds where they are
    Vector3 GetBoundsTarget()
    {
        if (_BoundsFollow != VABoundsFollow.Listener)
            return Position;

        return listener != null ? listener.GlobalPosition - _Size * 0.5f : boundsPosition;
    }

    void PlaceBounds()
    {
        boundsPosition = GetBoundsTarget();
        boundsListener = listener;
        world.Position = ToVAudio(boundsPosition);
    }

    void FollowBounds()
    {
        if (_BoundsFollow != VABoundsFollow.Listener)
            return;

        if (listener == null)
        {
            boundsListener = null;
            return;
        }

        // A new current listener gets centred bounds whatever BoundsUpdateDistance is
        if (listener != boundsListener)
        {
            PlaceBounds();
            return;
        }

        Vector3 target = GetBoundsTarget();

        // Every move of the bounds re-checks all raytraced trails, so they move in steps
        if (target != boundsPosition && target.DistanceSquaredTo(boundsPosition) >= _BoundsUpdateDistance * _BoundsUpdateDistance)
            PlaceBounds();
    }

    // With BoundsFollow Listener, the listener can move just under BoundsUpdateDistance along one axis before the bounds recentre, so each half of the box must be at least that wide
    Vector3 ClampBoundsSize(Vector3 size)
    {
        // Properties load in any order, so _EnterTree clamps the final values
        if (!IsInsideTree() || _BoundsFollow != VABoundsFollow.Listener)
            return size;

        float minimum = _BoundsUpdateDistance * 2.0f;
        return new(Mathf.Max(size.X, minimum), Mathf.Max(size.Y, minimum), Mathf.Max(size.Z, minimum));
    }

    void ReclampBoundsSize()
    {
        if (ClampBoundsSize(_Size) != _Size)
            Size = _Size;
    }

    Color _BoundsColor = new(0.0f, 0.0f, 0.0f, 0.25f);
    /// <summary>
    /// Editor-only color used to draw the world bounds gizmo in the 3D viewport
    /// </summary>
    [Export]
    public Color BoundsColor
    {
        get => _BoundsColor;
        set
        {
            _BoundsColor = value;
            UpdateGizmos();
        }
    }

    float _Epsilon = 0.01f;
    /// <summary>
    /// The epsilon value used for ray offsets, world bounds clamping and line-of-sight tests
    /// </summary>
    [Export]
    public float Epsilon
    {
        get => _Epsilon;
        set
        {
            _Epsilon = value;

            if (world != null)
                world.Epsilon = value;
        }
    }


    [ExportGroup("Layers")]

    uint _RenderLayers = 0xFFFFF;
    /// <summary>
    /// The render layers that should affect raytracing. A node only inherits a cascading material if its visual render layer is in this mask. A node with its own material is always included.
    /// </summary>
    [Export(PropertyHint.Layers3DRender)]
    public uint RenderLayers
    {
        get => _RenderLayers;
        set
        {
            if (_RenderLayers == value)
                return;

            _RenderLayers = value;
            RebuildPrimitives();
        }
    }

    uint _CollisionLayers = 0xFFFFF;
    /// <summary>
    /// The collision layers that should affect raytracing. A collider only inherits a cascading material if its body's collision layer is in this mask. A node with its own material is always included.
    /// </summary>
    [Export(PropertyHint.Layers3DPhysics)]
    public uint CollisionLayers
    {
        get => _CollisionLayers;
        set
        {
            if (_CollisionLayers == value)
                return;

            _CollisionLayers = value;
            RebuildPrimitives();
        }
    }


    [ExportGroup("OpenAL")]

    float _MasterVolume = 1;
    /// <summary>
    /// Master output volume, affects all sounds.
    /// If multiple VAWorlds exist, the last one to set this wins.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0,or_greater")]
    public float MasterVolume
    {
        get => _MasterVolume;
        set
        {
            _MasterVolume = MathF.Max(0, value);

            if (ALManager.Initialised)
                ALManager.MasterVolume = _MasterVolume;
        }
    }

    ALDistanceModel _DistanceModel = ALDistanceModel.InverseDistance;
    /// <summary>
    /// OpenAL distance attenuation model. 
    /// If multiple VAWorlds exist, the last one to set this wins.
    /// </summary>
    [Export]
    public ALDistanceModel DistanceModel
    {
        get => _DistanceModel;
        set
        {
            _DistanceModel = value;

            if (ALManager.Initialised)
                ALManager.DistanceModel = _DistanceModel;
        }
    }

    bool _ReverbOnly = false;
    /// <summary>
    /// When true, you'll only hear reverb - use for debugging.
    /// If multiple VAWorlds exist, the last one to set this wins.
    /// </summary>
    [Export]
    public bool ReverbOnly
    {
        get => _ReverbOnly;
        set
        {
            _ReverbOnly = value;

            if (ALManager.Initialised)
                ALManager.ReverbOnly = _ReverbOnly;
        }
    }



    [ExportGroup("Reverb")]

    int _MaximumGroupedEAXCount = 3;
    /// <summary>
    /// The maximum number of grouped EAX reverb properties created for all emitters. Higher values increase accuracy but are more expensive to run
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if &lt; 1</exception>
    [Export]
    public int MaximumGroupedEAXCount
    {
        get => _MaximumGroupedEAXCount;
        set
        {
            _MaximumGroupedEAXCount = Math.Max(0, value);

            if (world != null)
                world.MaximumGroupedEAXCount = _MaximumGroupedEAXCount;
        }
    }


    [ExportGroup("AirAbsorption")]

    float _MetersPerUnit = 1;
    /// <summary>
    /// Gets meters per world unit. Affects air absorption and reverb calculation.
    /// Also affects OpenAL's own air-absorption and reverb decay math.
    /// If multiple VAWorlds exist, the last one to set this wins.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if NaN, infinity or &lt;= 0</exception>
    [Export(PropertyHint.Range, "0.0001,1.0,or_greater")]
    public float MetersPerUnit
    {
        get => _MetersPerUnit;
        set
        {
            _MetersPerUnit = MathF.Max(0.0001f, value);

            if (world != null)
                world.MetersPerUnit = _MetersPerUnit;

            if (ALManager.Initialised)
                ALManager.MetersPerUnit = _MetersPerUnit;
        }
    }

    float _SpeedOfSound = 343.0f;

    /// <summary>
    /// Speed of sound in seconds per meter. Affects reverb calculation.
    /// Also affects OpenAL's own Doppler calculation.    
    /// If multiple VAWorlds exist, the last one to set this wins.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if NaN, infinity or &lt;= 0</exception>
    [Export(PropertyHint.Range, "0.0001,1000.0,1,or_greater")]
    public float SpeedOfSound
    {
        get => _SpeedOfSound;
        set
        {
            _SpeedOfSound = MathF.Max(0.0001f, value);

            if (world != null)
                world.InverseSpeedOfSound = 1.0f / _SpeedOfSound;

            if (ALManager.Initialised)
                ALManager.SpeedOfSound = _SpeedOfSound;
        }
    }

    float _Humidity = 0.1f;
    /// <summary>Humidity as a percentage</summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float Humidity
    {
        get => _Humidity;
        set
        {
            _Humidity = value;

            if (world != null)
                world.AirAbsorption.Humidity = _Humidity;
        }
    }

    float _Temperature = 26;
    /// <summary>Air temperature in degrees Celsius</summary>
    [Export(PropertyHint.Range, "-273.15f,100.0f,1,or_greater")]
    public float Temperature
    {
        get => _Temperature;
        set
        {
            _Temperature = value;

            if (world != null)
                world.AirAbsorption.Temperature = _Temperature;
        }
    }

    float _Pressure = 101325;
    /// <summary>Atmospheric pressure in Pascals</summary>
    [Export(PropertyHint.Range, "0.0,1000000,1,or_greater")]
    public float Pressure
    {
        get => _Pressure;
        set
        {
            _Pressure = value;

            if (world != null)
                world.AirAbsorption.Pressure = _Pressure;
        }
    }

    float _ReferenceFrequencyLF = 300;
    /// <summary>
    /// Low-frequency reference (Hz) for air absorption, reverb, and material scattering
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if NaN, infinity or &lt;= 0</exception>
    [Export(PropertyHint.Range, "0.0001,1000,1,or_greater")]
    public float ReferenceFrequencyLF
    {
        get => _ReferenceFrequencyLF;
        set
        {
            _ReferenceFrequencyLF = MathF.Max(0.0001f, value);

            if (world != null)
                world.ReferenceFrequencyLF = _ReferenceFrequencyLF;
        }
    }

    float _ReferenceFrequencyHF = 4000;
    /// <summary>
    /// High-frequency reference (Hz) for air absorption, reverb, and material scattering
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if NaN, infinity or &lt;= 0</exception>
    [Export(PropertyHint.Range, "0.0001,20000,1,or_greater")]
    public float ReferenceFrequencyHF
    {
        get => _ReferenceFrequencyHF;
        set
        {
            _ReferenceFrequencyHF = MathF.Max(0.0001f, value);

            if (world != null)
                world.ReferenceFrequencyHF = _ReferenceFrequencyHF;
        }
    }

    [ExportGroup("Emitters")]

    bool _EmittersOutsideTheWorldAreMuffled = true;
    /// <summary>
    /// If true, unclamped sources outside the bounds are fully muffled and stopped: one that doesn't loop isn't started there, and loops and streams play again once back inside. If false, they play unmuffled with no reverb.
    /// </summary>
    [Export]
    public bool EmittersOutsideTheWorldAreMuffled
    {
        get => _EmittersOutsideTheWorldAreMuffled;
        set
        {
            _EmittersOutsideTheWorldAreMuffled = value;

            if (world != null)
                world.EmittersOutsideTheWorldAreMuffled = value;
        }
    }

    bool _OcclusionRaysLoseEnergyFromWorldBounds = false;
    /// <summary>
    /// Whether occlusion rays should be affected by the world bounds material.
    /// </summary>
    [Export]
    public bool OcclusionRaysLoseEnergyFromWorldBounds
    {
        get => _OcclusionRaysLoseEnergyFromWorldBounds;
        set
        {
            _OcclusionRaysLoseEnergyFromWorldBounds = value;

            if (world != null)
                world.OcclusionRaysLoseEnergyFromWorldBounds = value;
        }
    }


    [ExportGroup("Threading")]

    // 0 maps to processor count - 1, matching the native plugin's behaviour
    int _MaximumConcurrencyLevel = 0;
    
    /// <summary>
    /// The maximum amount of threads that can run in parallel. Set to 0 to use the number of threads minus 1.
    /// </summary>
    [Export(PropertyHint.Range, "0,32,1,or_greater")]
    public int MaximumConcurrencyLevel
    {
        get => _MaximumConcurrencyLevel;
        set
        {
            _MaximumConcurrencyLevel = Math.Max(0, value);

            if (world != null)
                world.MaximumConcurrencyLevel = _MaximumConcurrencyLevel == 0 ? vaudio.ThreadStatistics.BackgroundThreadCount : _MaximumConcurrencyLevel;
        }
    }

    int _WorkItemCount = 128;
    /// <summary>
    /// The number of work items to split trails across for load balancing. A higher workItemCount helps evenly distribute work across all threads.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if &lt; 1</exception>
    [Export(PropertyHint.Range, "1,256,1,or_greater")]
    public int WorkItemCount
    {
        get => _WorkItemCount;
        set
        {
            _WorkItemCount = Math.Max(0, value);

            if (world != null)
                world.WorkItemCount = _WorkItemCount;
        }
    }

    bool _PendingShutdown = false;
    /// <summary>
    /// When set to true, <see cref="Update"/> will stop submitting work to background threads.<br/>
    /// When <see cref="ThreadsRunning"/> becomes false, it is safe to call <see cref="World.Dispose"/>.
    /// </summary>
    [Export]
    public bool PendingShutdown
    {
        get => _PendingShutdown;
        set
        {
            _PendingShutdown = value;

            if (world != null)
                world.PendingShutdown = value;
        }
    }


    [ExportGroup("Rendering")]

    bool _RenderingEnabled = true;
    /// <summary>Whether to render the raytracing simulation in a separate window.</summary>
    [Export]
    public bool RenderingEnabled
    {
        get => _RenderingEnabled;
        set
        {
            _RenderingEnabled = value;

            if (world == null)
                return;

            // Throws with the reason when the debug window failed to open - it opens in the World ctor, before LogCallback is set, so this is the only place that reason reaches Godot
            try
            {
                world.RenderingEnabled = _RenderingEnabled;
            }
            catch (InvalidOperationException e)
            {
                LogError(e.Message);
            }
        }
    }

    float _VisualisationScale = 1.0f;
    /// <summary>The size of visualisation rays in the debug window.</summary>
    [Export(PropertyHint.Range, "0.0,10.0,0.01,or_greater")]
    public float VisualisationScale
    {
        get => _VisualisationScale;
        set
        {
            _VisualisationScale = Math.Max(0, value);

            if (world != null)
                world.VisualisationScale = _VisualisationScale;
        }
    }

    /// <summary>
    /// While playing, mirrors the editor's 3D viewport camera into the debug window's camera - move
    /// the camera in the editor viewport and the debug window follows, instead of flying the debug
    /// window's own free-fly camera separately.
    /// </summary>
    [Export]
    public bool SyncViewport = true;
}
