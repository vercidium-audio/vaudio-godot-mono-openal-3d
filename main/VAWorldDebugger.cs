namespace vaudio_godot_mono_openal_3d;

public partial class VAWorld : Node3D
{
    const string DEBUGGER_CAPTURE_NAME = "vaudio";

    // Receiving end of VADebuggerPlugin.sync_primitive (editor/VADebuggerPlugin.gd) - the editor
    // process sends a "vaudio:sync_primitive" debugger message with the edited scene's root node
    // name and a NodePath relative to it, whenever the "Vercidium Audio" Inspector material
    // dropdown or use-flat-transmission checkbox changes while the game is running.
    // EditorInspectorPlugin controls can only edit the editor's own local copy of the scene, so
    // this debugger-message capture is the only way those edits reach the running game's actual
    // VAWorld.
    void RegisterDebuggerCapture()
    {
        // EngineDebugger only exists at all when running under the editor's debugger - there's no
        // running game to sync when running standalone (e.g. an exported build).
        if (!EngineDebugger.IsActive())
            return;

        if (!EngineDebugger.HasCapture(DEBUGGER_CAPTURE_NAME))
            EngineDebugger.RegisterMessageCapture(DEBUGGER_CAPTURE_NAME, Callable.From<string, Godot.Collections.Array, bool>(OnDebuggerMessage));
    }

    void UnregisterDebuggerCapture()
    {
        if (EngineDebugger.HasCapture(DEBUGGER_CAPTURE_NAME))
            EngineDebugger.UnregisterMessageCapture(DEBUGGER_CAPTURE_NAME);
    }

    // EngineDebugger strips the "vaudio:" prefix before calling this, so message here is just
    // "sync_primitive".
    bool OnDebuggerMessage(string message, Godot.Collections.Array data)
    {
        if (message != "sync_primitive" || data.Count < 4)
            return false;

        var treeRoot = GetTree()?.Root;
        if (treeRoot == null)
        {
            LogWarning("Received a material/use-flat-transmission edit from the editor, but the game has no scene tree root");
            return true;
        }

        var sceneRootName = data[0].As<string>();
        var sceneRoot = FindChildNamedRecursive(treeRoot, sceneRootName);

        if (sceneRoot == null)
        {
            LogWarning($"Received a material/use-flat-transmission edit from the editor, but no node named '{sceneRootName}' exists in the running scene");
            return true;
        }

        var nodePath = data[1].As<NodePath>();
        var node = sceneRoot.GetNodeOrNull(nodePath);

        if (node == null)
        {
            LogWarning($"Received a material/use-flat-transmission edit from the editor for '{nodePath}', but no matching node exists under '{sceneRootName}' ({sceneRoot.GetPath()})");
            return true;
        }

        // The running game has its own separate copy of this node - apply the metadata the
        // editor's local copy just had set/removed on it (see
        // VAMaterialInspectorPlugin._sync_running_game) before re-adding the primitive below,
        // since AddPrimitive/GetMaterial read it straight off this node, not off anything sent
        // directly.
        var material = data[2].As<string>();

        if (string.IsNullOrEmpty(material))
            node.RemoveMeta(MATERIAL_META_KEY);
        else
            node.SetMeta(MATERIAL_META_KEY, material);

        var useFlatTransmission = data[3];

        if (useFlatTransmission.VariantType == Variant.Type.Nil)
            node.RemoveMeta(USE_FLAT_TRANSMISSION_META_KEY);
        else
            node.SetMeta(USE_FLAT_TRANSMISSION_META_KEY, useFlatTransmission);

        SyncPrimitive(node);

        return true;
    }

    // Depth-first search for a descendant named name - used instead of SceneTree.CurrentScene,
    // which isn't reliable in a game that manually adds a scene as a plain child rather than via
    // ChangeSceneToFile/ChangeSceneToPacked, leaving CurrentScene permanently pointed elsewhere.
    static Node FindChildNamedRecursive(Node node, string name)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child.Name == name)
                return child;

            if (FindChildNamedRecursive(child, name) is Node found)
                return found;
        }

        return null;
    }
}
