// AI-created

namespace vaudio_godot_mono_openal;

partial class VAPrimitiveRef : RefCounted
{
    public vaudio.Primitive Primitive { get; set; }
    public TransformWatcher Watcher { get; set; }

    // The Shape or Mesh resource whose changed signal is connected, since editing it (size, radius, points etc) doesn't move the node
    Resource changedResource;
    Callable changedCallable;

    // The VAWorld.SharedMesh (3D) or VAWorld.SharedPolygon (2D) this primitive was created from, released in RemovePrimitive
    public object SharedGeometry { get; set; }

    public void ConnectChanged(Resource resource, Action onChanged)
    {
        changedResource = resource;
        changedCallable = Callable.From(onChanged);
        resource.Connect(Resource.SignalName.Changed, changedCallable);
    }

    public void DisconnectChanged()
    {
        if (changedResource != null && IsInstanceValid(changedResource) && changedResource.IsConnected(Resource.SignalName.Changed, changedCallable))
            changedResource.Disconnect(Resource.SignalName.Changed, changedCallable);

        changedResource = null;
    }
}
