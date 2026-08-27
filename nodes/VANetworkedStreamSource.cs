namespace vaudio_godot_mono_openal;

// A VAStreamSource intended to be fed by audio arriving over the network instead of a local input
// device - see VAInputStreamSource for the microphone equivalent. Adds nothing beyond
// VAStreamSource; a script's multiplayer/RPC/PacketPeerUdp handler should call PushAudioData
// directly on this node each time a chunk of PCM audio arrives.
[Tool]
[GlobalClass]
public partial class VANetworkedStreamSource : VAStreamSource
{
}
