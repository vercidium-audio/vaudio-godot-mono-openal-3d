namespace vaudio_godot_mono_openal;

partial class VAPrimitiveRef : RefCounted
{
    public vaudio.Primitive Primitive { get; set; }
    public TransformWatcher Watcher { get; set; }
    public Callable? ShapeCallable { get; set; }

    // The VAWorld.SharedMesh (3D) or VAWorld.SharedPolygon (2D) this primitive was created from, released in RemovePrimitive
    public object SharedGeometry { get; set; }
}
