namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    public const string PRIMITIVE_META_KEY = "vercidium_audio_primitive";
    public const string MATERIAL_META_KEY = "vercidium_audio_material";
    public const string USE_FLAT_TRANSMISSION_META_KEY = "vercidium_audio_use_flat_transmission";

    public vaudio.World world;
    public VAEmitter listener;

    public IAudioFilterHandle ambientFilter;

    // Reverb slots are backend-owned; the parallel DTOs are what CopyReverb / ApplyGroupedEAXPan write into before Push-ing to the slot.
    public IAudioReverbSlot listenerReverbSlot;
    public AudioReverbEffect listenerReverbEffect = new();

    public List<IAudioReverbSlot> groupedReverbSlots = [];
    public List<AudioReverbEffect> groupedReverbEffects = [];

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
