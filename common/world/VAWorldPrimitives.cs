namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    // Resync a node's primitives after its material / propagate / flat-transmission metadata changed in the editor.
    // The material that governs a node can live on an ancestor (e.g. a Node2D with material=concrete above a
    // StaticBody2D whose propagate mode is being edited), and the add walk only sees a material if it starts at
    // or above the node that owns it. Restarting from the node itself would leave the primitive removed and never
    // re-added, so walk up to the top-level scene node and resync the whole subtree from there.
    public void SyncPrimitive(Node node)
    {
        if (world == null)
            return;

        Node syncRoot = TopLevelSceneNode(node) ?? node;

        RemovePrimitive(syncRoot, true);
        AddPrimitive(syncRoot, vaudio.MaterialType.Air, false, PropagateMode.All, true);
    }

    // NodeAdded fires once per node rather than once per subtree, so a node inside an added subtree (e.g. an instanced scene whose root carries the material) has to pick up what its ancestors would have cascaded to it. Applied outermost first, the same order a recursive AddPrimitive cascades them in. Unknown materials aren't warned about here - they already were when their own node was added
    void ResolveInherited(Node node, out vaudio.MaterialType material, out bool useFlatTransmission, out PropagateMode filter)
    {
        material = vaudio.MaterialType.Air;
        useFlatTransmission = false;
        filter = PropagateMode.All;

        if (node.GetParent() is not { } parent)
            return;

        ResolveInherited(parent, out material, out useFlatTransmission, out filter);

        if (parent.HasMeta(MATERIAL_META_KEY))
            material = GetMaterial(parent, false);

        filter = ReadPropagateMode(parent, filter);

        if (parent.HasMeta(USE_FLAT_TRANSMISSION_META_KEY))
            useFlatTransmission = parent.GetMeta(USE_FLAT_TRANSMISSION_META_KEY).As<bool>();
    }

    // The highest ancestor of node that sits directly under the scene tree root, or null if node isn't under the tree
    static Node TopLevelSceneNode(Node node)
    {
        var tree = node.GetTree();
        Node root = tree?.Root;

        if (root == null)
            return null;

        Node current = node;

        while (current.GetParent() is { } parent && parent != root)
            current = parent;

        return current.GetParent() == root ? current : null;
    }
}
