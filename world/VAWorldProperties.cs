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

    // The bounds AABB is always axis-aligned starting at Position (see _Notification's
    // NotificationTransformChanged handler and VAWorldGizmoPlugin.gd), so rotation/scale would
    // be misleading in the Inspector - hide them, matching the native plugin's VAWorld.
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

    Vector3 _Size = new(200, 100, 200);
    /// <summary>
    /// The size of the world. <br />
    /// <see cref="VASource"/>s outside the world will not be raytraced, and Nodes that are fully outside these bounds will be ignored
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when worldSize is NaN, Infinity, or less than or equal to (0, 0, 0)</exception>
    [Export]
    public Vector3 Size
    {
        get => _Size;
        set
        {
            _Size = value;
            UpdateGizmos();

            if (world != null)
                world.Size = ToVAudio(value);
        }
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

    bool _WorldIsIndoors = false;
    /// <summary>
    /// Whether the entire world is considered indoors or outdoors. When false, reverb rays stop checking for line-of-sight after hitting the world edge
    /// </summary>
    [Export]
    public bool WorldIsIndoors
    {
        get => _WorldIsIndoors;
        set
        {
            _WorldIsIndoors = value;

            if (world != null)
                world.WorldIsIndoors = value;
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
    /// <exception cref="ArgumentException">Thrown when the value is less than 1</exception>
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
    /// <exception cref="ArgumentException">Thrown when the value is NaN, Infinity or less than or equal to 0</exception>
    /// </summary>
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
    /// Speed of sound in seconds per meter. Defaults to 343.0f. Affects reverb calculation.
    /// Also affects OpenAL's own Doppler calculation.    
    /// If multiple VAWorlds exist, the last one to set this wins.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is NaN, Infinity or less than or equal to 0</exception>
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
    /// <exception cref="ArgumentException">Thrown when the value is NaN or Infinity, or less than or equal to 0</exception>
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
    /// <exception cref="ArgumentException">Thrown when the value is NaN or Infinity, or less than or equal to 0</exception>
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
    /// Whether <see cref="VASource"/>s outside the world have 0 occlusion/permeation energy (true) or maximum energy (false).
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
    /// <exception cref="ArgumentException">Thrown when the value is less than 1</exception>
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
    /// When set to true, <see cref="Update"/> will stop submitting work to background threads. <br />
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
    /// <summary>Whether to render the raytracing scene in a separate window.</summary>
    [Export]
    public bool RenderingEnabled
    {
        get => _RenderingEnabled;
        set
        {
            _RenderingEnabled = value;

            if (world != null)
                world.RenderingEnabled = value;
        }
    }

    /// <summary>
    /// While playing, mirrors the editor's 3D viewport camera into the debug window's camera - move
    /// the camera in the editor viewport and the debug window follows, instead of flying the debug
    /// window's own F1 free-fly camera separately.
    /// </summary>
    [Export]
    public bool SyncViewport = true;
}
