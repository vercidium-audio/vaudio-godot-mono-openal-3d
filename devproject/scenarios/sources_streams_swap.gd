extends "res://scenarios/lib/scenario.gd"

# Reassigning a looping source's streams stops whatever was playing, like AudioStreamPlayer.set_stream(). Then clears the streams array in place while the new stream plays, which skips the setter, so playing again drops the decoded buffer while a voice still has it attached - stop() must then release it without an alDeleteBuffers error

const SEED := 20261017
const FREQUENCY := 44100

var source: Node
var baseline := 0

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)
	await wait_raytraced(SETTLE_PASSES)
	baseline = VA.grouped_eax_count(root.world)

func run() -> void:
	source = spawn_source("Swapped", root.source.position, SEED)
	await wait_raytraced_by_listener(source)
	check(await wait_playing(source), "source isn't playing its original stream")
	await step("original stream looping", 1.0)

	VA.set_value(source, "streams", [sine_wave(1.0)])
	check(not VA.is_playing(source), "original stream is still playing after reassigning streams")
	await step("streams reassigned - expect silence", 1.0)

	# The C# plugin's play() returns false while the new stream decodes, then starts it once decoded
	VA.play(source)
	check(await wait_playing(source), "source isn't playing the reassigned stream")
	await step("sine wave playing on its own", 1.0)

	VA.get_value(source, "streams").clear()
	VA.play(source)
	await step("streams cleared in place - sine wave keeps playing", 1.0)

	VA.stop(source)
	await wait_frames(2)
	check(not VA.is_playing(source), "source is still playing after stop()")

func teardown() -> void:
	if is_instance_valid(source):
		source.queue_free()

	# The freed source's reverb tail lingers, which would skew the next scenario's baseline
	await wait_grouped_eax_count(baseline)

# 16-bit mono 440 Hz sine wave - the source's own looping flag loops it in OpenAL
func sine_wave(seconds: float) -> AudioStreamWAV:
	var frames := int(FREQUENCY * seconds)
	var data := PackedByteArray()
	data.resize(frames * 2)
	for i in frames:
		data.encode_s16(i * 2, int(sin(TAU * 440.0 * i / FREQUENCY) * 8000.0))

	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = FREQUENCY
	wav.data = data
	return wav
