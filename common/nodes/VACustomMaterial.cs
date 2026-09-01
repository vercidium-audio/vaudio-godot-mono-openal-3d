namespace vaudio_godot_mono_openal;

/// <summary>
/// Custom acoustic material resource for the Vercidium Audio plugin.
/// Must be defined as a child Node of a VAWorld node.
/// Can be created in the Godot editor and assigned to collision shapes.
/// </summary>
[Tool]
[GlobalClass]
public partial class VACustomMaterial : Node
{
    VAWorld vercidiumAudio;
    vaudio.MaterialProperties vaudioMaterial;

    // Auto-assigned by VAWorld.RegisterCustomMaterial, not user-facing
    int materialType;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
            return;

        vercidiumAudio = this.GetVAWorldParent();

        // Custom materials register themselves at runtime
        if (vercidiumAudio == null)
        {
            Logger.LogWarning($"VACustomMaterial '{Name}' must be a direct child of a VAWorld node");
            return;
        }

        materialType = vercidiumAudio.RegisterCustomMaterial(this);

        vaudioMaterial = new vaudio.MaterialProperties(
            AbsorptionLF,
            AbsorptionHF,
            Scattering,
            TransmissionLF,
            TransmissionHF,
            FlatTransmissionLF,
            FlatTransmissionHF
        );

        vercidiumAudio.world.AddMaterial((vaudio.MaterialType)materialType, vaudioMaterial, GetDebugColor());
    }

    string _materialName = "CustomMaterial";

    /// <summary>
    /// Name of the custom material
    /// </summary>
    [Export(PropertyHint.None, "")]
    public string MaterialName
    {
        get => _materialName;
        set
        {
            _materialName = value;
            UpdateConfigurationWarnings();
        }
    }

    float _AbsorptionLF = 0.02f;
    float _AbsorptionHF = 0.1f;
    float _Scattering = 0.1f;
    float _TransmissionLF = 10;
    float _TransmissionHF = 5f;
    float _FlatTransmissionLF = 0.1f;
    float _FlatTransmissionHF = 0.25f;
    Color _DebugColor = new(1, 0, 1);

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

            if (vaudioMaterial != null)
                vaudioMaterial.AbsorptionLF = value;
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

            if (vaudioMaterial != null)
                vaudioMaterial.AbsorptionHF = value;
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

            if (vaudioMaterial != null)
                vaudioMaterial.Scattering = value;
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

            if (vaudioMaterial != null)
                vaudioMaterial.TransmissionLF = value;
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

            if (vaudioMaterial != null)
                vaudioMaterial.TransmissionHF = value;
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

            if (vaudioMaterial != null)
                vaudioMaterial.FlatTransmissionLF = value;
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

            if (vaudioMaterial != null)
                vaudioMaterial.FlatTransmissionHF = value;
        }
    }

    /// <summary>
    /// Debug color for the debug window
    /// </summary>
    [Export]
    public Color DebugColor
    {
        get => _DebugColor;
        set
        {
            _DebugColor = value;

            vercidiumAudio?.world.SetMaterialColor((vaudio.MaterialType)materialType, GetDebugColor());
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

        if (vaudioMaterial == null)
            return;

        vaudioMaterial.AbsorptionLF = _AbsorptionLF;
        vaudioMaterial.AbsorptionHF = _AbsorptionHF;
        vaudioMaterial.Scattering = _Scattering;
        vaudioMaterial.TransmissionLF = _TransmissionLF;
        vaudioMaterial.TransmissionHF = _TransmissionHF;
        vaudioMaterial.FlatTransmissionLF = _FlatTransmissionLF;
        vaudioMaterial.FlatTransmissionHF = _FlatTransmissionHF;

        vercidiumAudio?.world.SetMaterialColor((vaudio.MaterialType)materialType, GetDebugColor());
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(MaterialName))
            warnings.Add("Material Name should not be empty.");

        return [.. warnings];
    }
}
