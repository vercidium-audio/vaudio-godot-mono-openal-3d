namespace vaudio_godot_mono_openal;

// A grouped EAX's reverb effect, with the gain and pan last calculated for it, which it keeps while its EAX has no listener-relative results
internal class VAGroupedReverbEffect(VAReverbEffect effect)
{
    public readonly VAReverbEffect Effect = effect;
    public float Gain = 1;
    public Vector3 Pan;
}
