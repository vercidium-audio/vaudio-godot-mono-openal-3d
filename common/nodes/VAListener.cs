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

    public override void _ValidateProperty(Dictionary property)
    {
        base._ValidateProperty(property);

        string name = property["name"].AsStringName();

        // Hide irrelevant fields
        if (name == "HasRelativeReverb" || name == "AffectsGroupedEAX" || name == "OcclusionEnergyCap" || name == "PermeationEnergyCap" || name == "RaytraceOnce")
        {
            var usage = property["usage"].As<PropertyUsageFlags>();
            usage &= ~PropertyUsageFlags.Editor;
            property["usage"] = (int)usage;
        }
    }
}
