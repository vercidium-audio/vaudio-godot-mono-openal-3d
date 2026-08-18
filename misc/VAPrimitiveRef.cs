namespace vaudio_godot_mono_openal_3d;

partial class VAPrimitiveRef : RefCounted
{
    public vaudio.Primitive Primitive { get; set; }
    public TransformWatcher Watcher { get; set; }
    public Callable? ShapeCallable { get; set; }
}
