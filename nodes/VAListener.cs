using Godot.Collections;

namespace vaudio_godot_mono_openal;

// A purpose-named node for the world's single listener, rather than requiring users to add a
// plain VAEmitter and tick an IsMainListener checkbox themselves. Place exactly one of these
// under a VAWorld - every other VAEmitter/VASource is automatically added as one of its
// raytracing targets (see VAWorld.CreateEmitter).
//
// A subclass of VAEmitter rather than a from-scratch node - it reuses VAEmitter's create/destroy,
// listener-registration, and per-frame raytracing-result plumbing entirely. VAEmitter.IsMainListener
// is simply `this is VAListener`, so being this type is what makes a node the main listener.
// Matches the native plugin's VAListener.
[Tool]
[GlobalClass]
public partial class VAListener : VAEmitter
{
    public VAListener()
    {
        AffectsGroupedEAX = false;
        HasRelativeReverb = true;
    }

    // Hides inherited properties from the inspector that aren't meaningful on a listener:
    // AffectsGroupedEAX/OcclusionEnergyCap/PermeationEnergyCap only apply to emitters that can be
    // occluded/permeated, not the listener itself. RaytraceOnce only applies to emitters that get
    // raytraced against the listener, not the listener itself.
    public override void _ValidateProperty(Dictionary property)
    {
        base._ValidateProperty(property);

        string name = property["name"].AsStringName();

        if (name == "HasRelativeReverb" || name == "AffectsGroupedEAX" || name == "OcclusionEnergyCap" || name == "PermeationEnergyCap" || name == "RaytraceOnce")
        {
            var usage = property["usage"].As<PropertyUsageFlags>();
            usage &= ~PropertyUsageFlags.Editor;
            property["usage"] = (int)usage;
        }
    }
}
