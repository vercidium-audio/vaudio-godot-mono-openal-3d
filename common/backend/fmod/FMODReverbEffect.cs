// AI-created

namespace vaudio_godot_mono_openal;

// A reverb bus: a channel group whose RETURN DSP collects every voice's SEND, through an SFXREVERB DSP and, once it's panned, a PAN DSP. The group's volume is the effect's gain
sealed class FMODReverbEffect : VAReverbEffect
{
    readonly FMODBackend backend;
    FMOD.ChannelGroup group;
    FMOD.DSP reverb;
    FMOD.DSP returnDSP;
    FMOD.DSP pan;

    // The RETURN DSP's ID, which voices' SEND DSPs target
    internal int ReturnID { get; private set; } = -1;

    // The last value set per SFXREVERB parameter, so unchanged ones aren't set again
    readonly float[] reverbParams = new float[(int)FMOD.DSP_SFXREVERB.DRYLEVEL + 1];

    float lastGain = -1;
    Vector3 lastPan;
    internal FMODReverbEffect(FMODBackend backend)
    {
        this.backend = backend;

        if (!backend.IsInitialised)
            return;

        var system = backend.System;

        if (!backend.Check(system.createChannelGroup("vaudio reverb", out group), "System::createChannelGroup"))
            return;

        // Silent until SetParams sets the gain with the first EAX, so nothing plays through the default reverb
        backend.Check(group.setVolume(0), "ChannelGroup::setVolume");

        // TAIL is the input end, so this builds RETURN -> SFXREVERB -> fader
        if (!AddDSP(FMOD.DSP_TYPE.SFXREVERB, out reverb) || !AddDSP(FMOD.DSP_TYPE.RETURN, out returnDSP))
        {
            Dispose();
            return;
        }

        Array.Fill(reverbParams, float.NaN);
        SetReverbParam(FMOD.DSP_SFXREVERB.DRYLEVEL, FMODVoice.SilenceDecibels);

        if (backend.Check(returnDSP.getParameterInt((int)FMOD.DSP_RETURN.ID, out int id), "DSP::getParameterInt"))
            ReturnID = id;
        else
            Dispose();
    }

    bool AddDSP(FMOD.DSP_TYPE type, out FMOD.DSP dsp)
    {
        if (!backend.Check(backend.System.createDSPByType(type, out dsp), "System::createDSPByType"))
            return false;

        if (backend.Check(group.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, dsp), "ChannelGroup::addDSP"))
            return true;

        backend.Check(dsp.release(), "DSP::release");
        dsp.clearHandle();
        return false;
    }

    public override bool IsValid => group.hasHandle() && ReturnID >= 0 && backend.IsInitialised;

    public override void SetParams(vaudio.EAXReverb eax, float gain, Vector3 reflectionsPan, Vector3 lateReverbPan)
    {
        if (!IsValid)
            return;

        // Unit conversions checked against FMOD's presets vs their EFX equivalents. Clamped to FMOD's ranges, which are narrower than EAX's for the ratios
        SetReverbParam(FMOD.DSP_SFXREVERB.DECAYTIME, Math.Clamp(eax.DecayTime * 1000, 100, 20000));
        SetReverbParam(FMOD.DSP_SFXREVERB.EARLYDELAY, Math.Clamp(eax.ReflectionsDelay * 1000, 0, 300));
        SetReverbParam(FMOD.DSP_SFXREVERB.LATEDELAY, Math.Clamp(eax.LateReverbDelay * 1000, 0, 100));
        SetReverbParam(FMOD.DSP_SFXREVERB.HFREFERENCE, Math.Clamp(eax.HFReference, 20, 20000));
        SetReverbParam(FMOD.DSP_SFXREVERB.HFDECAYRATIO, Math.Clamp(eax.DecayHFRatio * 100, 10, 100));
        SetReverbParam(FMOD.DSP_SFXREVERB.DIFFUSION, Math.Clamp(eax.Diffusion * 100, 10, 100));
        SetReverbParam(FMOD.DSP_SFXREVERB.DENSITY, Math.Clamp(eax.Density * 100, 10, 100));
        SetReverbParam(FMOD.DSP_SFXREVERB.LOWSHELFFREQUENCY, Math.Clamp(eax.LFReference, 20, 1000));
        SetReverbParam(FMOD.DSP_SFXREVERB.LOWSHELFGAIN, Math.Clamp(eax.LowShelfGain, -36, 12));
        SetReverbParam(FMOD.DSP_SFXREVERB.HIGHCUT, Math.Clamp(eax.HighCut, 20, 20000));
        SetReverbParam(FMOD.DSP_SFXREVERB.EARLYLATEMIX, Math.Clamp(eax.EarlyLateMix, 0, 100));
        SetReverbParam(FMOD.DSP_SFXREVERB.WETLEVEL, Math.Clamp(eax.WetLevel, -80, 20));

        if (gain != lastGain)
        {
            lastGain = gain;
            backend.Check(group.setVolume(gain), "ChannelGroup::setVolume");
        }

        // SFXREVERB has one output, so the reflections and late reverb can't be panned separately. VAWorld passes the same pan for both today
        SetPan(lateReverbPan);
    }

    void SetReverbParam(FMOD.DSP_SFXREVERB param, float value)
    {
        if (reverbParams[(int)param] == value)
            return;

        reverbParams[(int)param] = value;
        backend.Check(reverb.setParameterFloat((int)param, value), "DSP::setParameterFloat");
    }

    // Like EFX's reverb pan: the direction (+X right, +Z forward) is where the reverb comes from, and the length (0-1) how focused it is there. The PAN DSP is only added once a pan is set, so the listener's effect never has one
    void SetPan(Vector3 direction)
    {
        if (direction == lastPan)
            return;

        lastPan = direction;

        if (!pan.hasHandle())
        {
            // HEAD is the output end, so the pan comes after the fader
            if (!backend.Check(backend.System.createDSPByType(FMOD.DSP_TYPE.PAN, out pan), "System::createDSPByType"))
                return;

            if (!backend.Check(group.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.HEAD, pan), "ChannelGroup::addDSP"))
            {
                backend.Check(pan.release(), "DSP::release");
                pan.clearHandle();
                return;
            }
        }

        float focus = Math.Clamp(new Vector2(direction.X, direction.Z).Length(), 0, 1);
        float degrees = focus > 0 ? MathF.Atan2(direction.X, direction.Z) * 180 / MathF.PI : 0;

        backend.Check(pan.setParameterFloat((int)FMOD.DSP_PAN._2D_DIRECTION, degrees), "DSP::setParameterFloat");
        backend.Check(pan.setParameterFloat((int)FMOD.DSP_PAN._2D_EXTENT, 360 * (1 - focus)), "DSP::setParameterFloat");
    }

    public override void Dispose()
    {
        // System::release already freed these with the device
        if (backend.IsInitialised)
        {
            foreach (var dsp in new[] { returnDSP, reverb, pan })
            {
                if (!dsp.hasHandle())
                    continue;

                if (group.hasHandle())
                    backend.Check(group.removeDSP(dsp), "ChannelGroup::removeDSP");

                backend.Check(dsp.release(), "DSP::release");
            }

            if (group.hasHandle())
                backend.Check(group.release(), "ChannelGroup::release");
        }

        group.clearHandle();
        reverb.clearHandle();
        returnDSP.clearHandle();
        pan.clearHandle();
        ReturnID = -1;
    }
}
