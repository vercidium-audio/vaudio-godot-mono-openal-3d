# AI-created

extends "res://scenarios/lib/scenario.gd"

# The FMOD backend loads the FMOD runtime and initialises (rather than falling back to OpenAL), then plays one sound through a VASourceRelative that doesn't wait for reverb. The FMOD export smoke tests run this on every platform, so it only checks the runtime loads and plays

var source: Node

func _init() -> void:
	requires_backends = ["fmod"]
	backend_smoke = true

func run() -> void:
	var backend := VA.backend_name(root.world)
	check(backend == "FMOD", "the audio backend is '%s', not FMOD" % backend)
	check(VA.is_backend_initialised(root.world), "the FMOD backend didn't initialise")

	source = VA.create_node(root.world, "VASourceRelative")
	source.name = "FMODSmoke"
	VA.set_value(source, "streams", VA.get_value(root.source, "streams"))
	VA.set_value(source, "use_listener_reverb", false)
	VA.set_value(source, "looping", true)
	VA.set_value(source, "autoplay", true)
	root.add_child(source)

	check(await wait_playing(source), "the sound never started playing through FMOD")
	await step("one sound playing through FMOD", 1.0)

func teardown() -> void:
	if is_instance_valid(source):
		source.queue_free()
