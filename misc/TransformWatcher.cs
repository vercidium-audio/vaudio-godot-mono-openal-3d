namespace vaudio_godot_mono_openal;

// Lightweight child node that fires OnTransformChanged when its parent's global transform changes.
// SetNotifyTransform is called here (not on the parent) so only this node gets the notification.
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
