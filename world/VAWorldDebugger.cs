namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    const string DEBUGGER_CAPTURE_NAME = "vaudio";

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

    const string DEBUGGER_PLUGIN_SINGLETON_NAME = "VADebuggerPlugin";

    void SendViewportCameraToRunningGame()
    {
        if (!Engine.HasSingleton(DEBUGGER_PLUGIN_SINGLETON_NAME))
            return;

        var viewport = EditorInterface.Singleton.GetEditorViewport3D(0);
        var camera = viewport?.GetCamera3D();

        if (camera == null)
            return;

        var debuggerRelay = Engine.GetSingleton(DEBUGGER_PLUGIN_SINGLETON_NAME);

        debuggerRelay.Call("sync_viewport_camera", camera.GlobalPosition, camera.GlobalRotation, camera.Fov);
    }

    // EngineDebugger strips the "vaudio:" prefix before calling this, so message here is just
    // "sync_primitive"/"sync_material_properties"/"sync_viewport_camera".
    bool OnDebuggerMessage(string message, Godot.Collections.Array data)
    {
        if (message == "sync_material_properties")
            return OnSyncMaterialProperties(data);

        if (message == "sync_viewport_camera")
            return OnSyncViewportCamera(data);

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

        // Propagation filter (optional - older editor builds send a 4-element payload)
        if (data.Count > 4)
        {
            var propagate = data[4].As<string>();

            if (string.IsNullOrEmpty(propagate))
                node.RemoveMeta(PROPAGATE_META_KEY);
            else
                node.SetMeta(PROPAGATE_META_KEY, propagate);
        }

        if (data.Count > 5)
        {
            var propagateLayer = data[5];

            if (propagateLayer.VariantType == Variant.Type.Nil)
                node.RemoveMeta(PROPAGATE_LAYER_META_KEY);
            else
                node.SetMeta(PROPAGATE_LAYER_META_KEY, propagateLayer);
        }

        SyncPrimitive(node);

        return true;
    }

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

    bool OnSyncViewportCamera(Godot.Collections.Array data)
    {
        if (data.Count < 3)
            return false;

        if (!SyncViewport || world == null || !world.RenderingEnabled)
            return true;

        var position = data[0].As<Vector3>();
        var rotation = data[1].As<Vector3>();
        var fovDegrees = data[2].As<float>();

        world.ManualCamera = false;
        world.CameraPosition = ToVAudio(position);
        world.CameraYaw = rotation.Y;
        world.CameraPitch = rotation.X;
        world.FieldOfView = float.DegreesToRadians(fovDegrees);

        return true;
    }

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
