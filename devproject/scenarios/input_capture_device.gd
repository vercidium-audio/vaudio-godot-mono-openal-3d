# AI-created

extends "res://scenarios/lib/scenario.gd"

# A VAInputStreamSource records from a real capture device through the audio backend and plays it back through its stream. The device list (what the inspector's Refresh Audio Devices button shows) comes from the backend, _ready() opens the stream and the capture, captured chunks arrive as whole frames at roughly the requested rate, and stop/start/close work. Skipped when the machine has no capture device

const SAMPLE_RATE := 44100
const FRAME_BYTES := 2 # mono 16-bit
const MEASURE_SECONDS := 1.0

var input: Node
var captured_bytes := 0
var partial_frames := 0

func _init() -> void:
	backend_smoke = true

func run() -> void:
	input = VA.create_node(root.world, "VAInputStreamSource")
	input.name = "CaptureInput"

	VA.refresh_capture_devices(input)
	var devices := String(VA.property_info(input, "device_name").get("hint_string", "")).split(",", false)
	devices.remove_at(0) # "System Default"
	print("[devproject] capture devices: %s" % ", ".join(devices))

	if devices.is_empty():
		skip("no capture device on this machine")
		return

	# By name rather than the system default, which some machines don't have (e.g. FMOD's loopback devices without a microphone)
	VA.set_value(input, "device_name", devices[0])
	VA.set_value(input, "format", VA.PCM_MONO16)
	VA.set_value(input, "sample_rate", SAMPLE_RATE)
	VA.connect_signal(input, "audio_captured", _on_audio_captured)
	root.add_child(input)

	check(VA.is_stream_open(input), "_ready didn't open the stream")
	check(VA.is_capture_open(input), "_ready didn't open the capture device '%s'" % devices[0])
	check(await wait_until(func(): return captured_bytes > 0), "nothing was captured from '%s'" % devices[0])

	var before := captured_bytes
	await step("'%s' playing back through the stream" % devices[0], MEASURE_SECONDS)
	var rate := (captured_bytes - before) / FRAME_BYTES / duration(MEASURE_SECONDS)
	print("[devproject] captured %d frames/s from '%s'" % [rate, devices[0]])
	check(rate > SAMPLE_RATE * 0.5 and rate < SAMPLE_RATE * 1.5, "captured %d frames/s, expected about %d" % [rate, SAMPLE_RATE])
	check(partial_frames == 0, "%d captured chunk(s) weren't whole frames" % partial_frames)

	# The last poll can still deliver what was recorded before the stop
	VA.stop_capture(input)
	await wait_frames(3)
	var stopped := captured_bytes
	await wait_frames(20)
	check(captured_bytes == stopped, "audio was still captured after stop_capture")

	VA.start_capture(input)
	check(await wait_until(func(): return captured_bytes > stopped), "nothing was captured after restarting")

	VA.close_capture(input)
	check(not VA.is_capture_open(input), "the capture is still open after close_capture")

func _on_audio_captured(data: PackedByteArray) -> void:
	captured_bytes += data.size()
	if data.size() % FRAME_BYTES != 0:
		partial_frames += 1

func teardown() -> void:
	if is_instance_valid(input):
		input.free()
