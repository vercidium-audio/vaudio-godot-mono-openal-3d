namespace vaudio_godot_mono_openal_3d;

public partial class VAWorld : Node3D
{
    public Dictionary<int, VAMaterial> customMaterials = [];

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
