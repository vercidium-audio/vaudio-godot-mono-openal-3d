// Human-reviewed

namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    // Resync a node's primitives after its material / propagate / flat-transmission metadata changed in the editor.
    // Ancestors may have changed, so walk up to the top-level scene node and resync the whole subtree from there.
    public void SyncPrimitive(Node node)
    {
        // world is only created in _EnterTree at runtime, so it's null in the editor or before this VAWorld enters the tree
        if (world == null)
        {
            LogWarning($"{Name}: SyncPrimitive({node.Name}) was called before the world was created, ignoring");
            return;
        }

        Node syncRoot = TopLevelSceneNode(node) ?? node;

        RemovePrimitive(syncRoot, true);
        AddPrimitive(syncRoot, vaudio.MaterialType.Air, false, PropagateMode.All, true);
    }

    // NodeAdded fires once per node rather than once per subtree, so a node inside an added subtree has to pick up what its ancestors would have cascaded to it
    void ResolveInherited(Node node, out vaudio.MaterialType material, out bool useFlatTransmission, out PropagateMode propagateMode)
    {
        material = vaudio.MaterialType.Air;
        useFlatTransmission = false;
        propagateMode = PropagateMode.All;

        // Recursion base case: the tree root Window has no parent, so there's nothing left to inherit from
        if (node.GetParent() is not { } parent)
            return;

        ResolveInherited(parent, out material, out useFlatTransmission, out propagateMode);

        if (parent.HasMeta(MATERIAL_META_KEY))
            material = GetMaterial(parent, false);

        propagateMode = ReadPropagateMode(parent, propagateMode);

        if (parent.HasMeta(USE_FLAT_TRANSMISSION_META_KEY))
            useFlatTransmission = parent.GetMeta(USE_FLAT_TRANSMISSION_META_KEY).As<bool>();
    }

    // Get the highest ancestor of a node that sits directly under the scene tree root, or null if node isn't under the tree
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
