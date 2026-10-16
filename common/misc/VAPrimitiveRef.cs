namespace vaudio_godot_mono_openal;

partial class VAPrimitiveRef : RefCounted
{
    public vaudio.Primitive Primitive { get; set; }
    public TransformWatcher Watcher { get; set; }
    public Callable? ShapeCallable { get; set; }

    // 3D only - the VAWorld.SharedMesh this MeshPrimitive was created from, released in RemovePrimitive
    public object SharedMesh { get; set; }
}
