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
    // "sync_primitive"/"sync_material_properties".
    bool OnDebuggerMessage(string message, Godot.Collections.Array data)
    {
        if (message == "sync_material_properties")
            return OnSyncMaterialProperties(data);

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

    // Relays a VADefaultMaterial/VACustomMaterial property edit made in the Inspector while the
    // game is running - see VAMaterialPropertiesInspectorPlugin.gd. Unlike sync_primitive above,
    // this never touches node metadata: the target node's own ApplyPropertiesFromEditor pushes the
    // new values straight into the vaudio.World material already tracking it.
    //
    // If the node doesn't exist yet, or exists but is a plain Node rather than a
    // VADefaultMaterial/VACustomMaterial, it's (re)created here so its own _EnterTree can register
    // it with the running vaudio.World the normal way. The "exists but plain Node" case isn't a
    // custom-protocol gap like sync_primitive's - Godot's own editor Live Edit already creates a
    // matching node in the running game whenever one is added in the editor's local scene copy, but
    // Live Edit only replicates the node's built-in Godot class, not any script attached to it
    // (VADefaultMaterial/VACustomMaterial are Nodes with a C# script, not distinct Godot classes),
    // so the node Live Edit creates is always a scriptless plain Node.
    bool OnSyncMaterialProperties(Godot.Collections.Array data)
    {
        // sceneRootName, nodePath, nodeName, isCustomMaterial, materialType, customMaterialName,
        // 7 material floats, debugColor
        if (data.Count < 14)
            return false;

        var treeRoot = GetTree()?.Root;
        if (treeRoot == null)
        {
            LogWarning("Received a material property edit from the editor, but the game has no scene tree root");
            return true;
        }

        var sceneRootName = data[0].As<string>();
        var sceneRoot = FindChildNamedRecursive(treeRoot, sceneRootName);

        if (sceneRoot == null)
        {
            LogWarning($"Received a material property edit from the editor, but no node named '{sceneRootName}' exists in the running scene");
            return true;
        }

        var nodePath = data[1].As<NodePath>();
        var node = sceneRoot.GetNodeOrNull(nodePath);

        var nodeName = data[2].As<string>();
        var isCustomMaterial = data[3].As<bool>();
        var materialType = data[4].As<int>();
        var customMaterialName = data[5].As<string>();

        float absorptionLf = data[6].As<float>();
        float absorptionHf = data[7].As<float>();
        float scattering = data[8].As<float>();
        float transmissionLf = data[9].As<float>();
        float transmissionHf = data[10].As<float>();
        float flatTransmissionLf = data[11].As<float>();
        float flatTransmissionHf = data[12].As<float>();
        var debugColor = data[13].As<Color>();

        if (node is not VADefaultMaterial and not VACustomMaterial)
            node = ReplaceWithMaterialNode(sceneRoot, nodePath, node, nodeName, isCustomMaterial, materialType, customMaterialName);

        if (node is VADefaultMaterial defaultMaterial)
            defaultMaterial.ApplyPropertiesFromEditor(absorptionLf, absorptionHf, scattering,
                transmissionLf, transmissionHf, flatTransmissionLf, flatTransmissionHf, debugColor);
        else if (node is VACustomMaterial customMaterial)
            customMaterial.ApplyPropertiesFromEditor(absorptionLf, absorptionHf, scattering,
                transmissionLf, transmissionHf, flatTransmissionLf, flatTransmissionHf, debugColor);
        else
            LogWarning($"Received a material property edit from the editor for '{nodePath}', but no matching VADefaultMaterial/VACustomMaterial node exists under '{sceneRootName}' ({sceneRoot.GetPath()}), and its parent VAWorld node doesn't exist in the running scene either - restart the running game to pick it up");

        return true;
    }

    // VADefaultMaterial/VACustomMaterial nodes are always direct children of a VAWorld node (see
    // both classes' doc comments). existingNode is either null (nothing at nodePath at all) or a
    // plain Node Live Edit already created there (see OnSyncMaterialProperties) - either way it's
    // replaced with a freshly constructed, correctly typed node under the same parent, so the new
    // node's own _EnterTree runs and registers it with the running vaudio.World. Returns null (with
    // no node created) if the parent isn't a VAWorld already present in the running scene.
    static Node ReplaceWithMaterialNode(Node sceneRoot, NodePath nodePath, Node existingNode, string nodeName,
        bool isCustomMaterial, int materialType, string customMaterialName)
    {
        Node parent;

        if (existingNode != null)
        {
            parent = existingNode.GetParent();
            parent?.RemoveChild(existingNode);
            existingNode.QueueFree();
        }
        else
        {
            int nameCount = nodePath.GetNameCount();
            if (nameCount < 2)
                return null;

            var parentSegments = new string[nameCount - 1];

            for (int i = 0; i < parentSegments.Length; i++)
                parentSegments[i] = nodePath.GetName(i);

            parent = sceneRoot.GetNodeOrNull(new NodePath(string.Join('/', parentSegments)));
        }

        if (parent is not VAWorld)
            return null;

        Node node;

        if (isCustomMaterial)
        {
            node = new VACustomMaterial { Name = nodeName, MaterialName = customMaterialName };
        }
        else
        {
            node = new VADefaultMaterial { Name = nodeName, MaterialType = (vaudio.MaterialType)materialType };
        }

        parent.AddChild(node);

        return node;
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
