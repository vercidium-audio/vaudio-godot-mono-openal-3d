namespace vaudio_godot_mono_openal;

/// <summary>
/// Backend-agnostic lowpass filter parameters. <see cref="gainHF"/> is absolute (not yet relative to <see cref="gain"/>); the backend's filter handle applies the relative-to-gain clamp that ALFilter.SetGain used to do.
/// </summary>
public readonly struct AudioFilter
{
    public readonly float gain;
    public readonly float gainHF;

    public AudioFilter(float gain, float gainHF)
    {
        this.gain = gain;
        this.gainHF = gainHF;
    }

    /// <summary>Fully attenuated - used for the direct path when only reverb should be audible.</summary>
    public static readonly AudioFilter Silent = new(0, 0);

    /// <summary>Unfiltered - used for the reverb send so the clear signal reaches the reverb.</summary>
    public static readonly AudioFilter Full = new(1, 1);
}
