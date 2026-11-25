# AI-created

extends "res://scenarios/lib/scenario.gd"

# VAInputStreamSource's settings, without opening a microphone. The node is never added to the tree, so _ready() doesn't start capturing. device_name is always a dropdown led by "System Default", even when the machine has no capture devices, and open_capture() rejects formats the microphone threshold can't read before touching the device

const DEFAULT_DEVICE_LABEL := "System Default"

var input: Node

func _init() -> void:
	backend_smoke = true

func run() -> void:
	input = VA.create_node(root.world, "VAInputStreamSource")
	input.name = "Input"

	var info := VA.property_info(input, "device_name")
	check(info.get("hint", -1) == PROPERTY_HINT_ENUM, "device_name isn't an enum dropdown")
	check(String(info.get("hint_string", "")).split(",")[0] == DEFAULT_DEVICE_LABEL, "device_name's dropdown doesn't start with '%s'" % DEFAULT_DEVICE_LABEL)

	check(VA.get_value(input, "format") == VA.PCM_MONO16, "format doesn't default to mono 16-bit")

	expect_warning("isn't a supported capture format")
	check(not VA.open_capture(input, "", VA.PCM_MONO_FLOAT, 44100, 1024), "open_capture accepted a float32 format")
	check(not VA.is_capture_open(input), "capture is open after open_capture rejected the format")

func teardown() -> void:
	if is_instance_valid(input):
		input.free()
