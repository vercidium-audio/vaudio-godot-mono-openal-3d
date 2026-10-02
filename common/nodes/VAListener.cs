using Godot.Collections;

namespace vaudio_godot_mono_openal;

[Tool]
[GlobalClass]
public partial class VAListener : VAEmitter
{
    public VAListener()
    {
        AffectsGroupedEAX = false;
        HasRelativeReverb = true;
    }

    bool _Current;
    /// <summary>
    /// If true, this is the world's current listener. The first listener added to a world becomes current automatically, and a listener added later with Current=true takes over. Setting this to false hands over to another listener in the world, unless this is the only one.
    /// </summary>
    [Export]
    public bool Current
    {
        get => _Current;
        set
        {
            // Not in a world yet (e.g. set while loading the scene, or in the editor) - VAWorld.RegisterListener reads the flag once this node is attached
            if (vercidiumAudio == null)
            {
                _Current = value;
                return;
            }

            if (value)
                vercidiumAudio.SetCurrentListener(this);
            else
                vercidiumAudio.ReleaseCurrentListener(this);
        }
    }

    /// <summary>Makes this the world's current listener. Same as setting <see cref="Current"/> to true.</summary>
    public void MakeCurrent() => Current = true;

    protected override void AttachToWorld() => vercidiumAudio.RegisterListener(this);

    protected override void DetachFromWorld() => vercidiumAudio.UnregisterListener(this);

    // Takes over the shared emitter from the previous listener, or creates it if this is the first listener in the world. Targets stay linked to the emitter.
    internal void Activate(vaudio.Emitter sharedEmitter)
    {
        _Current = true;

        if (sharedEmitter == null)
        {
            CreateEmitter();
            return;
        }

        emitter = sharedEmitter;
        ConfigureEmitter(emitter);
    }

    internal void Deactivate()
    {
        _Current = false;
        emitter = null;
    }

    // Only called when the last listener leaves the world
    internal void ReleaseSharedEmitter()
    {
        _Current = false;

        if (emitter != null)
            ReleaseEmitter();
    }

    public override void _ValidateProperty(Dictionary property)
    {
        base._ValidateProperty(property);

        string name = property["name"].AsStringName();

        // Hide irrelevant fields
        if (name == "HasRelativeReverb" || name == "AffectsGroupedEAX" || name == "KeepReverbTailAlive" ||name == "OcclusionEnergyCap" || name == "PermeationEnergyCap" || name == "RaytraceOnce")
        {
            var usage = property["usage"].As<PropertyUsageFlags>();
            usage &= ~PropertyUsageFlags.Editor;
            property["usage"] = (int)usage;
        }
    }
}
