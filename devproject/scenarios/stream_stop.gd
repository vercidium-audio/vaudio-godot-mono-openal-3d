extends "res://scenarios/lib/scenario.gd"

# Plays a sine wave through a VAStreamSource, then checks stop() closes the stream as well as stopping playback, so is_stream_open() doesn't stay true with nothing playing it. Reopening afterwards plays again, and close_stream() stops it too

const SEED := 20261007
const AL_FORMAT_MONO16 := 4353
const FREQUENCY := 44100

var stream: Node

func _init() -> void:
	scene = REVERB_ROOM

func run() -> void:
	stream = VA.create_node(root.world, "VAStreamSource")
	stream.name = "Stream"
	stream.position = root.source.position
	VA.set_value(stream, "reverb_ray_count", 64)
	VA.set_value(stream, "reverb_bounce_count", 64)
	VA.set_value(stream, "scattering_seed", SEED)
	root.add_child(stream)

	check(VA.open_stream(stream, AL_FORMAT_MONO16, FREQUENCY), "open_stream failed")
	await wait_raytraced_by_listener(stream)
	await wait_raytraced(SETTLE_PASSES)

	VA.push_audio_data(stream, sine_wave(1.0))
	check(await wait_playing(stream), "stream isn't playing after open_stream")
	await step("sine wave playing through the stream", 1.0)

	VA.stop(stream)
	check(not VA.is_stream_open(stream), "stream is still open after stop()")
	check(not VA.is_playing(stream), "stream is still playing after stop()")

	check(VA.open_stream(stream, AL_FORMAT_MONO16, FREQUENCY), "open_stream failed after stop()")
	check(VA.is_stream_open(stream), "stream isn't open after reopening")
	VA.push_audio_data(stream, sine_wave(1.0))
	check(await wait_playing(stream), "stream isn't playing after reopening")
	await step("sine wave playing through the reopened stream", 1.0)

	VA.close_stream(stream)
	check(not VA.is_stream_open(stream), "stream is still open after close_stream()")
	check(not VA.is_playing(stream), "stream is still playing after close_stream()")

func teardown() -> void:
	if is_instance_valid(stream):
		stream.queue_free()

# Signed 16-bit mono 440 Hz sine wave, little endian
func sine_wave(seconds: float) -> PackedByteArray:
	var frames := int(FREQUENCY * seconds)
	var data := PackedByteArray()
	data.resize(frames * 2)
	for i in frames:
		data.encode_s16(i * 2, int(sin(TAU * 440.0 * i / FREQUENCY) * 8000.0))
	return data
