// AI-created

namespace vaudio_godot_mono_openal;

// FMOD has no per-channel rolloff factor or OpenAL-style distance models, so each voice gets a custom rolloff curve sampled from the OpenAL formulas
static class FMODRolloff
{
    public const int PointCount = 32;

    // How far past the max distance the unclamped models keep falling, after which FMOD holds the last point's volume
    const float UnclampedRange = 10;

    const float MinReferenceDistance = 0.01f;

    // Volume is 1 up to the reference distance in every model, then the points spread out geometrically, since the inverse and exponent curves change fastest near the start
    public static unsafe void Fill(FMOD.VECTOR* points, VADistanceModel model, float referenceDistance, float maxDistance, float rolloff)
    {
        float start = MathF.Max(referenceDistance, MinReferenceDistance);
        float max = MathF.Max(maxDistance, start);
        float end = CurveEnd(model, start, max);

        points[0] = new() { x = 0, y = 1 };

        for (int i = 1; i < PointCount; i++)
        {
            float distance = start * MathF.Pow(end / start, (i - 1) / (float)(PointCount - 2));
            points[i] = new() { x = distance, y = Gain(model, distance, start, max, rolloff) };
        }
    }

    static float CurveEnd(VADistanceModel model, float start, float max)
    {
        float end = model is VADistanceModel.Inverse or VADistanceModel.Exponent ? max * UnclampedRange : max;
        return MathF.Max(end, start * 1.01f);
    }

    // The volume the voice's rolloff curve gives at this distance (ignoring FMOD's interpolation between its points), for things FMOD's rolloff doesn't reach, like a pre-fader reverb send
    public static float CurveGain(VADistanceModel model, float distance, float referenceDistance, float maxDistance, float rolloff)
    {
        float start = MathF.Max(referenceDistance, MinReferenceDistance);
        float max = MathF.Max(maxDistance, start);
        return Gain(model, MathF.Min(distance, CurveEnd(model, start, max)), start, max, rolloff);
    }

    // The OpenAL 1.1 distance models, clamped to 0-1 like OpenAL Soft's source gain
    static float Gain(VADistanceModel model, float distance, float reference, float max, float rolloff)
    {
        if (model is VADistanceModel.InverseClamped or VADistanceModel.LinearClamped or VADistanceModel.ExponentClamped or VADistanceModel.Linear)
            distance = Math.Clamp(distance, reference, max);

        float gain = model switch
        {
            VADistanceModel.None => 1,
            VADistanceModel.Inverse or VADistanceModel.InverseClamped => reference / (reference + rolloff * (distance - reference)),
            VADistanceModel.Linear or VADistanceModel.LinearClamped => max > reference ? 1 - rolloff * (distance - reference) / (max - reference) : 1,
            _ => MathF.Pow(distance / reference, -rolloff),
        };

        return float.IsFinite(gain) ? Math.Clamp(gain, 0, 1) : 0;
    }
}
