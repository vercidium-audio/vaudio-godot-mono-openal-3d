// AI-created

using System.Collections.Concurrent;

namespace vaudio_godot_mono_openal;

// Raw PCM chunks pushed on the main thread and read by a backend's audio thread, which gets silence when the queue runs dry
sealed class VAStreamQueue(VAPcmFormat format)
{
    readonly ConcurrentQueue<ArraySegment<byte>> chunks = new();

    // 8-bit PCM is unsigned, so its silence is 0x80
    readonly byte silence = (byte)(format.BytesPerSample() == 1 ? 0x80 : 0);

    // The chunk being read. Only touched by the reading thread
    ArraySegment<byte> current;

    // The array must not be changed until it's been played
    public void Enqueue(byte[] data, int offset, int length)
    {
        if (length > 0)
            chunks.Enqueue(new(data, offset, length));
    }

    // Fills the destination with queued audio, then silence
    public unsafe void Read(byte* destination, int count)
    {
        var write = new Span<byte>(destination, count);

        while (write.Length > 0)
        {
            if (current.Count == 0 && !chunks.TryDequeue(out current))
                break;

            int bytes = Math.Min(current.Count, write.Length);
            current.AsSpan(0, bytes).CopyTo(write);
            current = current.Slice(bytes);
            write = write.Slice(bytes);
        }

        write.Fill(silence);
    }
}
