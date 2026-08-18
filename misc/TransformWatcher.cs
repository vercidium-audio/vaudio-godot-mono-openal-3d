namespace vaudio_godot_mono_openal_3d;

/// <summary>
/// A lightweight child node that fires a callback whenever its parent's global transform changes.
/// Attach to any Node3D, then set OnTransformChanged. Call SetNotifyTransform(true) here (not on the parent)
/// so only this node receives the notification, avoiding double-processing.
/// </summary>
partial class TransformWatcher : Node3D
{
    public Action OnTransformChanged { get; set; }

    public override void _Ready()
    {
        SetNotifyTransform(true);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationTransformChanged)
            OnTransformChanged?.Invoke();
    }
}
