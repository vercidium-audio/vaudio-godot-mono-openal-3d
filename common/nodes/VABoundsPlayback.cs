// Human-reviewed

namespace vaudio_godot_mono_openal;

// Autoplay and world bounds rules shared by VASource, VASourceLeech and VASourceRelative, which have no raytraced base class in common. VASourceRelative is never silenced
internal class VABoundsPlayback
{
    // A Play() that hasn't started yet: waiting for the emitter to be ready, a loop waiting for the bounds, or one the bounds stopped
    bool playPending;

    // The time in seconds the queued play starts from
    internal float PendingStartTime { get; private set; }

    // Set once played or cancelled, so autoplay only starts it once
    internal bool AutoplayDone;

    internal void OnPlayed()
    {
        playPending = false;
        PendingStartTime = 0;
        AutoplayDone = true;
    }

    // Stop() cancels any queued play, as if it had played
    internal void Cancel() => OnPlayed();

    // Call at the top of Play(). Returns whether to play now, otherwise the play is queued until it's ready, and a loop waits until it's inside the bounds. A sound that doesn't loop isn't played outside the bounds, with a warning
    internal bool RequestPlay(VAPlayer player, bool silenced, bool ready, float startTime)
    {
        if (silenced && !player.Looping)
            LogWarning($"{player.Name}: This source did not play because it is outside the world bounds.");

        if (!silenced && ready)
            return true;

        // Waits for the emitter's muffling and reverb results, so the sound never starts unmuffled or without reverb. StopIfSilenced() cancels it if it doesn't loop and is outside the bounds
        playPending = true;
        PendingStartTime = startTime;
        StopIfSilenced(player, silenced);
        return false;
    }

    // Call every frame. While silenced it stops the sound, re-queues a loop that was playing so it resumes inside, and cancels a queued play that doesn't loop. Returns whether to call Play(PendingStartTime) now, for a queued play or autoplay
    internal bool Process(VAPlayer player, bool silenced, bool ready, bool autoplay)
    {
        if (StopIfSilenced(player, silenced))
            return false;

        return ready && (playPending || (autoplay && !AutoplayDone));
    }

    // Returns whether it's silenced
    bool StopIfSilenced(VAPlayer player, bool silenced)
    {
        if (!silenced)
            return false;

        if (!player.Looping)
            Cancel();
        // A paused loop is re-queued too, and starts paused once inside. It restarts from the beginning, unless a play from a start time is already queued
        else if (player.HasSound() || player.IsPlayRequested)
        {
            if (!playPending)
                PendingStartTime = 0;

            playPending = true;
        }

        player.StopPlayback();
        return true;
    }
}
