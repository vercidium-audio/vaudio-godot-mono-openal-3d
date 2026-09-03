namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public const string PRIMITIVE_META_KEY = "vercidium_audio_primitive";
    public const string MATERIAL_META_KEY = "vercidium_audio_material";
    public const string USE_FLAT_TRANSMISSION_META_KEY = "vercidium_audio_use_flat_transmission";

    // Constrains which descendants a cascading material applies to. Set on an ancestor; a node
    // with its own MATERIAL_META_KEY ignores an inherited filter and resets it for its subtree.
    public const string PROPAGATE_META_KEY = "vercidium_audio_propagate";

    public enum PropagateMode { All, Colliders, Visuals }

    public readonly record struct PropagateFilter(PropagateMode Mode)
    {
        public static readonly PropagateFilter Default = new(PropagateMode.All);
    }

    protected static PropagateFilter ReadPropagateFilter(Node node, PropagateFilter inherited)
    {
        if (!node.HasMeta(PROPAGATE_META_KEY))
            return inherited;

        return node.GetMeta(PROPAGATE_META_KEY).As<string>().ToLowerInvariant() switch
        {
            "colliders" => inherited with { Mode = PropagateMode.Colliders },
            "visuals" => inherited with { Mode = PropagateMode.Visuals },
            _ => inherited with { Mode = PropagateMode.All },
        };
    }

    public vaudio.World world;
    public VAEmitter listener;

    public ALFilter ambientFilter;
    public ALReverbEffect listenerReverbEffect;
    public List<ALReverbEffect> groupedReverbEffects = [];

    Dictionary<string, vaudio.MaterialType> DefaultMaterialDict = new()
    {
        { "air", vaudio.MaterialType.Air },
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
}
