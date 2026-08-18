using System.Linq;
using Godot.Collections;
namespace vaudio_godot_mono_openal_3d;

/// <summary>
/// Custom acoustic material resource for the Vercidium Audio plugin.
/// Must be defined as a child Node of a VAWorld node.
/// Can be created in the Godot editor and assigned to collision shapes.
/// </summary>
[Tool]
[GlobalClass]
public partial class VAMaterial : Node
{
    private int _materialType = 1000;
    private string _materialName = "CustomMaterial";

    VAWorld vercidiumAudio;
    vaudio.MaterialProperties vaudioMaterial;

    System.Collections.Generic.Dictionary<string, vaudio.MaterialType> DefaultMaterialNames = new()
    {
        { "brick", vaudio.MaterialType.Brick },
        { "cloth", vaudio.MaterialType.Cloth },
        { "concrete", vaudio.MaterialType.Concrete },
        { "concretepolished", vaudio.MaterialType.ConcretePolished },
        { "dirt", vaudio.MaterialType.Dirt },
        { "glass", vaudio.MaterialType.Glass },
        { "grass", vaudio.MaterialType.Grass },
        { "gravel", vaudio.MaterialType.Gravel },
        { "gyprock", vaudio.MaterialType.Gyprock },
        { "ice", vaudio.MaterialType.Ice },
        { "leaf", vaudio.MaterialType.Leaf },
        { "marble", vaudio.MaterialType.Marble },
        { "metal", vaudio.MaterialType.Metal },
        { "mud", vaudio.MaterialType.Mud },
        { "rock", vaudio.MaterialType.Rock },
        { "sand", vaudio.MaterialType.Sand },
        { "snow", vaudio.MaterialType.Snow },
        { "tile", vaudio.MaterialType.Tile },
        { "tree", vaudio.MaterialType.Tree },
        { "water", vaudio.MaterialType.Water },
        { "woodindoor", vaudio.MaterialType.WoodIndoor },
        { "woodoutdoor", vaudio.MaterialType.WoodOutdoor },
    };

    string DefaultMaterialNameHint => string.Join(",", DefaultMaterialNames.Keys);

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
            return;

        vercidiumAudio = this.GetVAWorldParent();

        if (IsDefaultMaterial)
        {
            var mat = vercidiumAudio.world.GetMaterial(defaultMaterialType);

            mat.AbsorptionLF = AbsorptionLF;
            mat.AbsorptionHF = AbsorptionHF;
            mat.Scattering = Scattering;
            mat.TransmissionLF = TransmissionLF;
            mat.TransmissionHF = TransmissionHF;
            mat.FlatTransmissionLF = FlatTransmissionLF;
            mat.FlatTransmissionHF = FlatTransmissionHF;

            vercidiumAudio.world.SetMaterialColor(defaultMaterialType, GetDebugColor());
            return;
        }

        // Prevent duplicates
        if (vercidiumAudio.customMaterials.TryGetValue(MaterialType, out VAMaterial value))
        {
            vercidiumAudio.LogError($"The VAMaterial node {Name} has the same material ID ({MaterialType}) as the VAMaterial node {value.Name}. Please change this to another ID");
            return;
        }

        // Create the vaudio material
        vaudioMaterial = new vaudio.MaterialProperties(
            AbsorptionLF,
            AbsorptionHF,
            Scattering,
            TransmissionLF,
            TransmissionHF,
            FlatTransmissionLF,
            FlatTransmissionHF
        );

        vercidiumAudio.world.AddMaterial((vaudio.MaterialType)MaterialType, vaudioMaterial, GetDebugColor());
        vercidiumAudio.customMaterials[MaterialType] = this;
    }


    vaudio.MaterialType defaultMaterialType = vaudio.MaterialType.Air;
    bool _isDefaultMaterial = false;

    [Export(PropertyHint.None, "")]
    public bool IsDefaultMaterial
    {
        get => _isDefaultMaterial;
        set
        {
            if (value == _isDefaultMaterial)
                return;

            _isDefaultMaterial = value;

            if (value)
            {
                // Switching to a default material: pick the first entry and reuse its name
                var firstEntry = DefaultMaterialNames.First();
                defaultMaterialType = firstEntry.Value;
                _materialName = firstEntry.Key;
            }
            else
            {
                defaultMaterialType = vaudio.MaterialType.Air;
            }

            UpdateConfigurationWarnings();
            NotifyPropertyListChanged();
        }
    }


    bool firstTypeSet = true;

    /// <summary>
    /// Unique material ID. Must be >= 1000 to avoid conflicts with built-in materials
    /// </summary>
    [Export(PropertyHint.Range, "1000,9999,1,or_greater,no_slider")]
    public int MaterialType
    {
        get => _materialType;
        set
        {
            if (!firstTypeSet && !Engine.IsEditorHint())
            {
                vercidiumAudio?.LogWarning($"Cannot change the type of VAMaterial nodes at runtime. Node: {Name}");
                return;
            }

            _materialType = value;
            UpdateConfigurationWarnings();

            firstTypeSet = false;
        }
    }

    bool firstNameSet = true;

    /// <summary>
    /// Material name for debugging and identification
    /// </summary>
    [Export(PropertyHint.None, "")]
    public string MaterialName
    {
        get => _materialName;
        set
        {
            if (!firstNameSet && !Engine.IsEditorHint())
            {
                vercidiumAudio?.LogWarning($"Cannot change the name of VAMaterial nodes at runtime. Node: {Name}");
                return;
            }

            // Update field
            _materialName = value;

            if (IsDefaultMaterial)
                DefaultMaterialNames.TryGetValue(value, out defaultMaterialType);

            UpdateConfigurationWarnings();

            firstNameSet = false;
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
    /// Low-frequency absorption coefficient (0.0 to 1.0)
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float AbsorptionLF
    { 
        get => _AbsorptionLF;
        set
        {
            // Prevent redundant sets
            if (value == _AbsorptionLF)
                return;

            _AbsorptionLF = value;

            if (vercidiumAudio != null)
                vaudioMaterial.AbsorptionLF = value;
        }
    }

    /// <summary>
    /// High-frequency absorption coefficient (0.0 to 1.0)
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float AbsorptionHF
    {
        get => _AbsorptionHF;
        set
        {
            // Prevent redundant sets
            if (value == _AbsorptionHF)
                return;

            _AbsorptionHF = value;

            if (vercidiumAudio != null)
                vaudioMaterial.AbsorptionHF = value;
        }
    }

    /// <summary>
    /// Scattering coefficient (0.0 to 1.0)
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float Scattering
    {
        get => _Scattering;
        set
        {
            // Prevent redundant sets
            if (value == _Scattering)
                return;

            _Scattering = value;

            if (vercidiumAudio != null)
                vaudioMaterial.Scattering = value;
        }
    }

    /// <summary>
    /// Low-frequency transmission in dB/m (0.0 or greater)
    /// </summary>
    [Export(PropertyHint.Range, "0.0001f,10.0,0.001f,or_greater")]
    public float TransmissionLF
    {
        get => _TransmissionLF;
        set
        {
            // Prevent redundant sets
            if (value == _TransmissionLF)
                return;

            _TransmissionLF = value;

            if (vercidiumAudio != null)
                vaudioMaterial.TransmissionLF = value;
        }
    }

    /// <summary>
    /// High-frequency transmission in dB/m (0.0 or greater)
    /// </summary>
    [Export(PropertyHint.Range, "0.0001f,10.0,0.001f,or_greater")]
    public float TransmissionHF
    {
        get => _TransmissionHF;
        set
        {
            // Prevent redundant sets
            if (value == _TransmissionHF)
                return;

            _TransmissionHF = value;

            if (vercidiumAudio != null)
                vaudioMaterial.TransmissionHF = value;
        }
    }

    /// <summary>
    /// Percentage of low-frequency energy lost when a ray passes through a flat primitive
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float FlatTransmissionLF
    {
        get => _FlatTransmissionLF;
        set
        {
            // Prevent redundant sets
            if (value == _FlatTransmissionLF)
                return;

            _FlatTransmissionLF = value;

            if (vercidiumAudio != null)
                vaudioMaterial.FlatTransmissionLF = value;
        }
    }

    /// <summary>
    /// Percentage of high-frequency energy lost when a ray passes through a flat primitive
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float FlatTransmissionHF
    {
        get => _FlatTransmissionHF;
        set
        {
            // Prevent redundant sets
            if (value == _FlatTransmissionHF)
                return;

            _FlatTransmissionHF = value;

            if (vercidiumAudio != null)
                vaudioMaterial.FlatTransmissionHF = value;
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

            vercidiumAudio?.world.SetMaterialColor((vaudio.MaterialType)MaterialType, GetDebugColor());
        }
    }

    /// <summary>
    /// Gets the debug color as a vaudio.Color
    /// </summary>
    public vaudio.Color GetDebugColor() => new(DebugColor.R, DebugColor.G, DebugColor.B, 1.0f);

    /// <summary>
    /// Validates the material configuration and returns warnings
    /// </summary>
    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (MaterialType < 1000)
        {
            warnings.Add($"Material ID must be >= 1000 (current: {MaterialType}). IDs 0-999 are reserved for built-in materials.");
        }

        if (string.IsNullOrWhiteSpace(MaterialName))
        {
            warnings.Add("Material Name should not be empty.");
        }

        return [.. warnings];
    }

    public override void _ValidateProperty(Dictionary property)
    {
        string name = property["name"].AsStringName();

        if (name == "MaterialType")
        {
            var usage = property["usage"].As<PropertyUsageFlags>();

            if (IsDefaultMaterial)
                usage &= ~PropertyUsageFlags.Editor;
            else
                usage |= PropertyUsageFlags.Editor;

            property["usage"] = (int)(usage);
        }
        else if (name == "MaterialName")
        {
            if (IsDefaultMaterial)
            {
                property["hint"] = (int)PropertyHint.Enum;
                property["hint_string"] = DefaultMaterialNameHint;
            }
            else
            {
                property["hint"] = (int)PropertyHint.None;
                property["hint_string"] = "";
            }
        }
    }
}
