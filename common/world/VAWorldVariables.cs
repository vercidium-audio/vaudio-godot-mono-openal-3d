namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public const string PRIMITIVE_META_KEY = "vercidium_audio_primitive";
    public const string MATERIAL_META_KEY = "vercidium_audio_material";
    public const string USE_FLAT_TRANSMISSION_META_KEY = "vercidium_audio_use_flat_transmission";

    // Constrains which descendants a cascading material applies to. Set on an ancestor; a node
    // with its own MATERIAL_META_KEY ignores an inherited filter and resets it for its subtree.
    public const string PROPAGATE_META_KEY = "vercidium_audio_propagate";
    public const string PROPAGATE_LAYER_META_KEY = "vercidium_audio_propagate_layer";

    public enum PropagateMode { All, Colliders, Visuals }

    // Layer 0 means "no layer restriction"
    public readonly record struct PropagateFilter(PropagateMode Mode, uint Layer)
    {
        public static readonly PropagateFilter Default = new(PropagateMode.All, 0);
    }

    protected static PropagateFilter ReadPropagateFilter(Node node, PropagateFilter inherited)
    {
        var filter = inherited;

        if (node.HasMeta(PROPAGATE_META_KEY))
        {
            filter = node.GetMeta(PROPAGATE_META_KEY).As<string>().ToLowerInvariant() switch
            {
                "colliders" => filter with { Mode = PropagateMode.Colliders },
                "visuals" => filter with { Mode = PropagateMode.Visuals },
                _ => filter with { Mode = PropagateMode.All },
            };
        }

        if (node.HasMeta(PROPAGATE_LAYER_META_KEY))
            filter = filter with { Layer = (uint)node.GetMeta(PROPAGATE_LAYER_META_KEY).As<int>() };

        return filter;
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
