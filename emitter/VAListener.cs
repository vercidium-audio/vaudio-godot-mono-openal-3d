using Godot.Collections;

namespace vaudio_godot_mono_openal_3d;

// A purpose-named node for the world's single listener, rather than requiring users to add a
// plain VAEmitter and tick its IsMainListener checkbox themselves. Place exactly one of these
// under a VAWorld - every other VAEmitter/VASource is automatically added as one of its
// raytracing targets (see VAWorld.CreateEmitter).
//
// A subclass of VAEmitter rather than a from-scratch node - it reuses VAEmitter's create/destroy,
// listener-registration, and per-frame raytracing-result plumbing entirely, and only forces
// IsMainListener to true (hidden from the inspector via _ValidateProperty, since it's not
// meaningful to toggle off on this class). Matches the native plugin's VAListener.
[Tool]
[GlobalClass]
public partial class VAListener : VAEmitter
{
    public VAListener()
    {
        IsMainListener = true;
        HasRelativeReverb = true;
    }

    // Hides inherited properties from the inspector that aren't meaningful on a listener:
    // IsMainListener is always true (see the constructor), and AffectsGroupedEAX/
    // OcclusionEnergyCap/PermeationEnergyCap only apply to emitters that can be occluded/permeated,
    // not the listener itself.
    public override void _ValidateProperty(Dictionary property)
    {
        base._ValidateProperty(property);

        string name = property["name"].AsStringName();

        if (name == "IsMainListener" || name == "AffectsGroupedEAX" || name == "OcclusionEnergyCap" || name == "PermeationEnergyCap")
        {
            var usage = property["usage"].As<PropertyUsageFlags>();
            usage &= ~PropertyUsageFlags.Editor;
            property["usage"] = (int)usage;
        }
    }
}
