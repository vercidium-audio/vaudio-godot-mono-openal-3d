namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    const int FirstCustomMaterialId = 1000;

    public Dictionary<int, VACustomMaterial> customMaterials = [];

    /// <summary>
    /// Assigns a VACustomMaterial the lowest material ID not already claimed by another custom
    /// material in this world, starting at FirstCustomMaterialId
    /// </summary>
    public int RegisterCustomMaterial(VACustomMaterial material)
    {
        int type = FirstCustomMaterialId;

        foreach (var id in customMaterials.Keys)
            if (id >= type)
                type = id + 1;

        customMaterials[type] = material;
        return type;
    }

    /// <summary>
    /// Extract a material type from a node. Returns MaterialType.Air if no materials are found
    /// </summary>
    vaudio.MaterialType GetMaterial(Node node)
    {
        if (!node.HasMeta(MATERIAL_META_KEY))
            return vaudio.MaterialType.Air;

        var materialString = node.GetMeta(MATERIAL_META_KEY).As<string>();

        // Match custom materials
        foreach (var kvp in customMaterials)
            if (kvp.Value.MaterialName.Equals(materialString, StringComparison.CurrentCultureIgnoreCase))
                return (vaudio.MaterialType)kvp.Key;

        // Match default materials
        if (DefaultMaterialDict.TryGetValue(materialString, out var type))
            return type;

        // No material found
        LogWarning($"Unknown material for node {node.Name}: {materialString}. Defaulting to Air");
        return vaudio.MaterialType.Air;
    }
}
