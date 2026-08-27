namespace vaudio_godot_mono_openal;

public static unsafe partial class ALManager
{
    static void DestroyAllAudioSources(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is ALSource source)
                source.OnDeviceDestroyed();

            DestroyAllAudioSources(child);
        }
    }

    public static void DestroyAll()
    {
        // Sanity check
        if (ALDevice == null || ALContext == null)
        {
            Debug.Assert(false);
            return;
        }

        // Delete sources before effects - no Node.GetTree() to reach the scene tree from a static
        // class, so go via the main loop directly (same pattern Ensure() uses).
        DestroyAllAudioSources(((SceneTree)Engine.GetMainLoop()).Root);

        // Invoke device destroyed callbacks (e.g. for cleaning up reverb effects)
        foreach (var callback in OnDeviceDestroyedCallbacks)
            callback.Invoke();

        // Delete context
        AL.MakeContextCurrent(IntPtr.Zero);
        ALContext.Destroy();
        ALContext = null;

        // Delete device
        ALDevice.Close();
        ALDevice = null;
    }

    public static void CancelLoadingAndDestroy()
    {
        // Tell the background sound-loading threads to stop loading
        ALBuffer.CancelLoadingSounds = true;

        // Wait for all threads to finish
        foreach (var buffer in DecodedStreams.Values)
            buffer.WaitForTask();

        DecodedStreams.Clear();
        ALBuffer.CancelLoadingSounds = false;

        // Delete everything - unfortunately we can't copy data from buffers in one OpenAL context to another. We need to re-decode every AudioStream :(
        // RecreateDevice() (ALManagerDevice.cs) only falls back to this when ALDevice.Reopen
        // (ALC_SOFT_reopen_device) isn't available on the current device.
        DestroyAll();
    }
}
