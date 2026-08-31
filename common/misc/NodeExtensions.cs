namespace vaudio_godot_mono_openal;

public static class NodeExtensions
{
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
