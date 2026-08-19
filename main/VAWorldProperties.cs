using Godot.Collections;

namespace vaudio_godot_mono_openal_3d;

public partial class VAWorld : Node3D
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
    [Export]
    /// <summary>
    /// The size of the world. <br />
    /// <see cref="Emitter"/>s outside the world will not be raytraced, and <see cref="Primitive"/>s that are fully outside these bounds will be ignored
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when worldSize is NaN, Infinity, or less than or equal to (0, 0, 0)</exception>
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
    [Export]
    /// <summary>
    /// Editor-only color used to draw the world bounds gizmo in the 3D viewport. Not forwarded to vaudio.
    /// </summary>
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
    [Export]
    /// <summary>
    /// The epsilon value used for ray offsets, world bounds clamping and line-of-sight tests. Defaults to 0.01f
    /// </summary>
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

    bool _WorldIsIndoors = true;
    [Export]
    /// <summary>
    /// Whether the entire world is considered indoors or outdoors. When false, reverb rays stop checking for line-of-sight after hitting the world edge. Defaults to false.
    /// </summary>
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


    [ExportGroup("Reverb")]

    int _MaximumGroupedEAXCount = 3;
    [Export]
    /// <summary>
    /// The maximum number of grouped EAX reverb properties created for all emitters. Higher values increase accuracy but are more expensive to run. Defaults to 3
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is less than 1</exception>
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
    [Export(PropertyHint.Range, "0.0001,1.0,or_greater")]
    /// <summary>
    /// Gets meters per world unit. Affects air absorption and reverb calculation.
    /// <exception cref="ArgumentException">Thrown when the value is NaN, Infinity or less than or equal to 0</exception>
    /// </summary>
    public float MetersPerUnit
    {
        get => _MetersPerUnit;
        set
        {
            _MetersPerUnit = MathF.Max(0.0001f, value);

            if (world != null)
                world.MetersPerUnit = _MetersPerUnit;
        }
    }

    float _SpeedOfSound = 343.0f;

    [Export(PropertyHint.Range, "0.0001,1000.0,1,or_greater")]
    /// <summary>
    /// Speed of sound in seconds per meter. Defaults to 343.0f. Affects reverb calculation
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is NaN, Infinity or less than or equal to 0</exception>
    public float SpeedOfSound
    {
        get => _SpeedOfSound;
        set
        {
            _SpeedOfSound = MathF.Max(0.0001f, value);

            if (world != null)
                world.InverseSpeedOfSound = 1.0f / _SpeedOfSound;
        }
    }

    float _Humidity = 0.1f;
    [Export(PropertyHint.Range, "0.0,1.0")]
    /// <summary>Relative humidity as a percentage (0–1). Defaults to 0.1f</summary>
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
    [Export(PropertyHint.Range, "-273.15f,100.0f,1,or_greater")]
    /// <summary>Air temperature in degrees Celsius. Defaults to 26</summary>
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
    [Export(PropertyHint.Range, "0.0,1000000,1,or_greater")]
    /// <summary>Atmospheric pressure in Pascals. Defaults to 101325</summary>
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
    [Export(PropertyHint.Range, "0.0001,1000,1,or_greater")]
    /// <summary>
    /// Low-frequency reference (Hz) for air absorption, reverb, and material scattering
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is NaN or Infinity, or less than or equal to 0</exception>
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
    [Export(PropertyHint.Range, "0.0001,20000,1,or_greater")]
    /// <summary>
    /// High-frequency reference (Hz) for air absorption, reverb, and material scattering
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is NaN or Infinity, or less than or equal to 0</exception>
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
    [Export]
    /// <summary>
    /// Whether <see cref="Emitter"/>s outside the world have 0 occlusion/permeation energy (true) or maximum energy (false).
    /// </summary>
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
    [Export(PropertyHint.Range, "1,256,1,or_greater")]
    /// <summary>
    /// The number of work items to split trails across for load balancing. A higher workItemCount helps evenly distribute work across all threads.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is less than 1</exception>
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
    [Export]
    /// <summary>
    /// When set to true, <see cref="Update"/> will stop submitting work to background threads. <br />
    /// When <see cref="ThreadsRunning"/> becomes false, it is safe to call <see cref="World.Dispose"/>.
    /// </summary>
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
    [Export]
    /// <summary>Whether to render the raytracing scene in a separate window. Only one world can have rendering enabled</summary>
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
}
