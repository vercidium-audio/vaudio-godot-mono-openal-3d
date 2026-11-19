// Human-created

namespace vaudio_godot_mono_openal;

/// <summary>
/// Node that fires OnTransformChanged when its parent's global transform changes
/// </summary>
partial class TransformWatcher : Node3D
{
    public Action OnTransformChanged { get; set; }

    public override void _Ready()
    {
        // Called here instead of on the parent, so only this node gets the notification
        SetNotifyTransform(true);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationTransformChanged)
            OnTransformChanged?.Invoke();
    }
}
