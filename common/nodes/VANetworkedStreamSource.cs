namespace vaudio_godot_mono_openal;

// A VAStreamSource fed by audio arriving over the network - a script's multiplayer/RPC/PacketPeerUdp handler calls PushAudioData on this node per chunk. See VAInputStreamSource for the microphone equivalent.
[Tool]
[GlobalClass]
public partial class VANetworkedStreamSource : VAStreamSource
{
}
