namespace vaudio_godot_mono_openal;

public partial class VAWorld
{
    // 3D implementation of common/world/VAWorldReverb.cs's ApplyGroupedEAXPan - derives the
    // listener-relative pan from the listener's pitch and yaw.
    partial void ApplyGroupedEAXPan(vaudio.EAXReverb eax, ALReverbEffect effect)
    {
        if (eax.RelativeDirections == null || !eax.RelativeDirections.TryGetValue(listener.emitter, out var pan))
            return;

        // Convert to a listener-relative vector for OpenAL
        Vector3 listenerRotation = listener.GlobalRotation;
        pan = world.CalculateListenerRelativePan(pan, listenerRotation.X, listenerRotation.Y);

        effect.effectSlotGain = eax.RelativeGains[listener.emitter];
        effect.effectSlotGain = Math.Max(0, effect.effectSlotGain);
        effect.effectSlotGain = Math.Min(1, effect.effectSlotGain);

        // TODO - separate pan for late reverb and reflections
        effect.lateReverbPan[0] = pan.X;
        effect.lateReverbPan[1] = pan.Y;
        effect.lateReverbPan[2] = pan.Z;

        effect.reflectionsPan[0] = pan.X;
        effect.reflectionsPan[1] = pan.Y;
        effect.reflectionsPan[2] = pan.Z;
    }
}
