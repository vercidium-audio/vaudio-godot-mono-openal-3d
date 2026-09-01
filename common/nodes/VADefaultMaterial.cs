namespace vaudio_godot_mono_openal;

/// <summary>
/// Overrides the properties of one of the SDK's built-in materials.
/// Must be defined as a child Node of a VAWorld node.
/// </summary>
[Tool]
[GlobalClass]
public partial class VADefaultMaterial : Node
{
    static System.Collections.Generic.Dictionary<vaudio.MaterialType, MaterialDefaults> cachedDefaults;

    class MaterialDefaults
    {
        public float AbsorptionLF, AbsorptionHF, Scattering, TransmissionLF, TransmissionHF, FlatTransmissionLF, FlatTransmissionHF;
        public Color Color;
    }

    // Reads the SDK's built-in default properties for every material type from a scratch World.
    // World() never calls Update(), so poolInternal stays null and Dispose() never spins up threads.
    static MaterialDefaults GetMaterialDefaults(vaudio.MaterialType type)
    {
        if (cachedDefaults == null)
        {
            cachedDefaults = [];

            var scratch = new vaudio.World();

            foreach (vaudio.MaterialType materialType in System.Enum.GetValues<vaudio.MaterialType>())
            {
                var props = scratch.GetMaterial(materialType);

                cachedDefaults[materialType] = new MaterialDefaults
                {
                    AbsorptionLF = props.AbsorptionLF,
                    AbsorptionHF = props.AbsorptionHF,
                    Scattering = props.Scattering,
                    TransmissionLF = props.TransmissionLF,
                    TransmissionHF = props.TransmissionHF,
                    FlatTransmissionLF = props.FlatTransmissionLF,
                    FlatTransmissionHF = props.FlatTransmissionHF,
                    Color = ToGodotColor(scratch.GetMaterialColor(materialType)),
                };
            }

            scratch.Dispose();
        }

        return cachedDefaults[type];
    }

    static Color ToGodotColor(vaudio.Color color) => new(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, 1.0f);

    VAWorld vercidiumAudio;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
            return;

        vercidiumAudio = this.GetVAWorldParent();

        if (vercidiumAudio == null)
        {
            Logger.LogWarning($"VADefaultMaterial '{Name}' must be a direct child of a VAWorld node");
            return;
        }

        var mat = vercidiumAudio.world.GetMaterial(MaterialType);

        mat.AbsorptionLF = AbsorptionLF;
        mat.AbsorptionHF = AbsorptionHF;
        mat.Scattering = Scattering;
        mat.TransmissionLF = TransmissionLF;
        mat.TransmissionHF = TransmissionHF;
        mat.FlatTransmissionLF = FlatTransmissionLF;
        mat.FlatTransmissionHF = FlatTransmissionHF;

        vercidiumAudio.world.SetMaterialColor(MaterialType, GetDebugColor());
    }

    vaudio.MaterialType _materialType = vaudio.MaterialType.Metal;

    /// <summary>
    /// The default materials to override
    /// </summary>
    [Export]
    public vaudio.MaterialType MaterialType
    {
        get => _materialType;
        set
        {
            if (value == _materialType)
                return;

            _materialType = value;

            // Switching to a new material type: reset all properties to that material's own defaults
            var defaults = GetMaterialDefaults(value);

            _AbsorptionLF = defaults.AbsorptionLF;
            _AbsorptionHF = defaults.AbsorptionHF;
            _Scattering = defaults.Scattering;
            _TransmissionLF = defaults.TransmissionLF;
            _TransmissionHF = defaults.TransmissionHF;
            _FlatTransmissionLF = defaults.FlatTransmissionLF;
            _FlatTransmissionHF = defaults.FlatTransmissionHF;
            _DebugColor = defaults.Color;

            if (vercidiumAudio != null)
            {
                var mat = vercidiumAudio.world.GetMaterial(value);

                mat.AbsorptionLF = _AbsorptionLF;
                mat.AbsorptionHF = _AbsorptionHF;
                mat.Scattering = _Scattering;
                mat.TransmissionLF = _TransmissionLF;
                mat.TransmissionHF = _TransmissionHF;
                mat.FlatTransmissionLF = _FlatTransmissionLF;
                mat.FlatTransmissionHF = _FlatTransmissionHF;

                vercidiumAudio.world.SetMaterialColor(value, GetDebugColor());
            }

            NotifyPropertyListChanged();
        }
    }

    float _AbsorptionLF = 0.05f;
    float _AbsorptionHF = 0.02f;
    float _Scattering = 0.01f;
    float _TransmissionLF = 0.1f;
    float _TransmissionHF = 0.05f;
    float _FlatTransmissionLF = 0.1f;
    float _FlatTransmissionHF = 0.25f;
    Color _DebugColor = new(1, 1, 1);

    /// <summary>
    /// Percentage of low-frequency energy that is lost on each bounce
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float AbsorptionLF
    {
        get => _AbsorptionLF;
        set
        {
            if (value == _AbsorptionLF)
                return;

            _AbsorptionLF = value;

            if (vercidiumAudio != null)
                vercidiumAudio.world.GetMaterial(MaterialType).AbsorptionLF = value;
        }
    }

    /// <summary>
    /// Percentage of low-frequency energy that is lost on each bounce
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float AbsorptionHF
    {
        get => _AbsorptionHF;
        set
        {
            if (value == _AbsorptionHF)
                return;

            _AbsorptionHF = value;

            if (vercidiumAudio != null)
                vercidiumAudio.world.GetMaterial(MaterialType).AbsorptionHF = value;
        }
    }

    /// <summary>
    /// Scattering strength, where 0.0 has no scattering and 1.0 skews the ray reflection direction by up to 90 degrees
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float Scattering
    {
        get => _Scattering;
        set
        {
            if (value == _Scattering)
                return;

            _Scattering = value;

            if (vercidiumAudio != null)
                vercidiumAudio.world.GetMaterial(MaterialType).Scattering = value;
        }
    }

    /// <summary>
    /// How many meters a ray must travel through a primitive before it loses all low-frequency energy
    /// </summary>
    [Export(PropertyHint.Range, "0.0001f,10.0,0.001f,or_greater")]
    public float TransmissionLF
    {
        get => _TransmissionLF;
        set
        {
            if (value == _TransmissionLF)
                return;

            _TransmissionLF = value;

            if (vercidiumAudio != null)
                vercidiumAudio.world.GetMaterial(MaterialType).TransmissionLF = value;
        }
    }

    /// <summary>
    /// How many meters a ray must travel through a primitive before it loses all high-frequency energy
    /// </summary>
    [Export(PropertyHint.Range, "0.0001f,10.0,0.001f,or_greater")]
    public float TransmissionHF
    {
        get => _TransmissionHF;
        set
        {
            if (value == _TransmissionHF)
                return;

            _TransmissionHF = value;

            if (vercidiumAudio != null)
                vercidiumAudio.world.GetMaterial(MaterialType).TransmissionHF = value;
        }
    }

    /// <summary>
    /// Percentage of low-frequency energy that is lost when a ray passes through a flat primitive
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float FlatTransmissionLF
    {
        get => _FlatTransmissionLF;
        set
        {
            if (value == _FlatTransmissionLF)
                return;

            _FlatTransmissionLF = value;

            if (vercidiumAudio != null)
                vercidiumAudio.world.GetMaterial(MaterialType).FlatTransmissionLF = value;
        }
    }

    /// <summary>
    /// Percentage of high-frequency energy that is lost when a ray passes through a flat primitive
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float FlatTransmissionHF
    {
        get => _FlatTransmissionHF;
        set
        {
            if (value == _FlatTransmissionHF)
                return;

            _FlatTransmissionHF = value;

            if (vercidiumAudio != null)
                vercidiumAudio.world.GetMaterial(MaterialType).FlatTransmissionHF = value;
        }
    }

    /// <summary>
    /// Debug color for the VAudio debug renderer
    /// </summary>
    [Export]
    public Color DebugColor
    {
        get => _DebugColor;
        set
        {
            _DebugColor = value;

            vercidiumAudio?.world.SetMaterialColor(MaterialType, GetDebugColor());
        }
    }

    /// <summary>
    /// Gets the debug color as a vaudio.Color
    /// </summary>
    public vaudio.Color GetDebugColor() => new(DebugColor.R, DebugColor.G, DebugColor.B, 1.0f);

    public void ApplyPropertiesFromEditor(float absorptionLf, float absorptionHf, float scattering,
                                          float transmissionLf, float transmissionHf,
                                          float flatTransmissionLf, float flatTransmissionHf, Color debugColor)
    {
        _AbsorptionLF = absorptionLf;
        _AbsorptionHF = absorptionHf;
        _Scattering = scattering;
        _TransmissionLF = transmissionLf;
        _TransmissionHF = transmissionHf;
        _FlatTransmissionLF = flatTransmissionLf;
        _FlatTransmissionHF = flatTransmissionHf;
        _DebugColor = debugColor;

        if (vercidiumAudio == null)
            return;

        var mat = vercidiumAudio.world.GetMaterial(MaterialType);

        mat.AbsorptionLF = _AbsorptionLF;
        mat.AbsorptionHF = _AbsorptionHF;
        mat.Scattering = _Scattering;
        mat.TransmissionLF = _TransmissionLF;
        mat.TransmissionHF = _TransmissionHF;
        mat.FlatTransmissionLF = _FlatTransmissionLF;
        mat.FlatTransmissionHF = _FlatTransmissionHF;

        vercidiumAudio.world.SetMaterialColor(MaterialType, GetDebugColor());
    }
}
