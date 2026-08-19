using System.Linq;

namespace vaudio_godot_mono_openal_3d;

public static class NodeExtensions
{
    public static VAWorld GetVAWorldParent(this Node node)
    {
        var sceneRoot = node.GetTree().CurrentScene;
        if (sceneRoot == null)
            return null;

        return sceneRoot.GetChildren().OfType<VAWorld>().FirstOrDefault();
    }
}