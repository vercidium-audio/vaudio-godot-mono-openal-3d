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
        AddPrimitive(syncRoot, vaudio.MaterialType.Air, true, PropagateMode.All, true);
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
