namespace vaudio_godot_mono_openal;

// Autoplay and world bounds rules shared by VASource and VASourceLeech, which have no raytraced base class in common
internal class VABoundsPlayback
{
    // A Play() that hasn't started yet: a loop waiting for the bounds, or one the bounds stopped
    bool playPending;

    // Set once played or cancelled, so autoplay only starts it once
    internal bool AutoplayDone;

    internal bool IsPending(bool autoplay) => playPending || (autoplay && !AutoplayDone);

    internal void OnPlayed()
    {
        playPending = false;
        AutoplayDone = true;
    }

    // Stop() cancels any waiting play, as if it had played
    internal void Cancel() => OnPlayed();

    // Call every frame before autoplay. While silenced it stops the sound, re-arms a loop that was playing so it resumes inside, and cancels a pending play that doesn't loop. Returns whether it's silenced
    internal bool Apply(ALSource source, bool silenced)
    {
        if (!silenced)
            return false;

        if (!source.Looping)
            Cancel();
        else if (source.IsPlaying() || source.IsPlayRequested)
            playPending = true;

        source.StopPlayback();
        return true;
    }

    // Call at the top of Play(). Returns whether the bounds stopped it from playing, warning for a sound that doesn't loop. A loop waits and plays once inside
    internal bool ApplyOnPlay(ALSource source, bool silenced)
    {
        if (!silenced)
            return false;

        if (!source.Looping)
            LogWarning($"{source.Name}: Play() was called outside the VAWorld's bounds, so nothing played. A sound that doesn't loop isn't started outside them, and doesn't play once back inside");

        playPending = true;
        return Apply(source, silenced);
    }
}
