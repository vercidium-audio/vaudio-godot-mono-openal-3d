namespace vaudio_godot_mono_openal;

// Mirrors the native plugin's VAWorld getters, so GDScript (e.g. the devproject tests) can read world statistics without touching the vaudio.World field
public partial class VAWorld
{
    public double GetMainThreadTime() => world?.MainThreadTime ?? 0;
    public double GetPreparationTime() => world?.PreparationTime ?? 0;
    public double GetRaytracingTime() => world?.RaytracingTime ?? 0;
    public double GetAnalysisTime() => world?.AnalysisTime ?? 0;

    // Number of completed raytracing passes, so tests can wait for fresh results after changing the scene. No signal on purpose - it would fire inside world.Update(), and a handler that edits the world would re-enter it
    int raytraceCount;
    public int GetRaytraceCount() => raytraceCount;

    public int GetGroupedEAXCount() => world?.GroupedEAX.Count ?? 0;
    public float GetGroupedEAXGainLF(int index) => world.GroupedEAX[index].GainLF;
    public float GetGroupedEAXGainHF(int index) => world.GroupedEAX[index].GainHF;
    public float GetGroupedEAXDecayTime(int index) => world.GroupedEAX[index].DecayTime;

    /// <summary>
    /// Exports all world settings, materials, primitives, and emitters to a binary file (dev build only). Returns false if the export failed
    /// </summary>
    public bool ExportToFile(string filePath)
    {
        try
        {
            world.Export(filePath);
            return true;
        }
        catch (Exception e)
        {
            LogError($"Failed to export the world to '{filePath}': {e.Message}");
            return false;
        }
    }
}
