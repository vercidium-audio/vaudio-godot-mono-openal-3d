namespace vaudio_godot_mono_openal;

public static class NodeExtensions
{
    // Scans the whole tree from its root, not just direct children of CurrentScene - matches the
    // native plugin's find_va_world, so a VAWorld is found regardless of scene nesting or whether
    // this node's scene was added as a sibling of CurrentScene rather than a child of it.
    public static VAWorld GetVAWorldParent(this Node node)
    {
        var root = node.GetTree()?.Root;
        if (root == null)
            return null;

        return FindVAWorldRecursive(root);
    }

    static VAWorld FindVAWorldRecursive(Node node)
    {
        if (node is VAWorld world)
            return world;

        foreach (var child in node.GetChildren())
        {
            var found = FindVAWorldRecursive(child);
            if (found != null)
                return found;
        }

        return null;
    }

    // Looks for a VAWorld immediately; if none exists yet (or it exists but hasn't finished its
    // own _EnterTree yet - world is only assigned there, not in the constructor), retries on every
    // future node addition instead of giving up, so a VAWorld/VAListener added later (e.g. from
    // another .tscn) is still picked up. Calls onFound once a fully-initialised VAWorld is located,
    // and returns an action the caller must invoke from its own _ExitTree to clean up the pending
    // connection if this node leaves the tree before that happens.
    public static Action WaitForVAWorld(this Node node, Action<VAWorld> onFound)
    {
        var world = node.GetVAWorldParent();

        if (world != null && world.Initialised)
        {
            onFound(world);
            return null;
        }

        var tree = node.GetTree();
        if (tree == null)
            return null;

        void Retry(Node _)
        {
            var found = node.GetVAWorldParent();
            if (found == null || !found.Initialised)
                return;

            tree.NodeAdded -= Retry;
            onFound(found);
        }

        tree.NodeAdded += Retry;

        return () => tree.NodeAdded -= Retry;
    }
}
