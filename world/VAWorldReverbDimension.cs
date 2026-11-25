// Human-reviewed

namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    // Where a grouped EAX's reverb comes from relative to the listener, derived from the listener's pitch and yaw. Used by common/world/VAWorldReverb.cs
    Vector3 GetGroupedEAXPan(vaudio.EAXReverb eax)
    {
        Vector3 listenerRotation = listener.GlobalRotation;
        var pan = world.CalculateListenerRelativePan(eax.GetRelativeDirection(listener.emitter), listenerRotation.X, listenerRotation.Y);

        return new Vector3(pan.X, pan.Y, pan.Z);
    }
}
