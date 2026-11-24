extends "res://scenarios/lib/scenario.gd"

# A VASourceLeech under a VAEmitter that casts no reverb rays follows the emitter's use_listener_reverb: no reverb when off, the listener's reverb when on, and it follows the setting when it's toggled while playing

var plain_emitter: Node
var reverb_emitter: Node
var plain: Node
var reverb: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	plain_emitter = add_emitter("LeechNoReverbEmitter", false)
	reverb_emitter = add_emitter("LeechListenerReverbEmitter", true)
	plain = add_leech("LeechNoReverb", plain_emitter)
	reverb = add_leech("LeechListenerReverb", reverb_emitter)

	check(await wait_until(func(): return VA.is_ready_to_play(plain) and VA.is_ready_to_play(reverb)), "leeches never became ready to play")

func run() -> void:
	check(await wait_playing(plain), "leech under an emitter without use_listener_reverb never started")
	check(await wait_playing(reverb), "leech under an emitter with use_listener_reverb never started")
	await wait_frames(2)
	check(not VA.has_reverb(plain), "leech under an emitter without use_listener_reverb has a reverb effect")
	check(VA.has_reverb(reverb), "leech under an emitter with use_listener_reverb has no reverb effect")

	# Toggled on the emitters while playing
	VA.set_value(plain_emitter, "use_listener_reverb", true)
	VA.set_value(reverb_emitter, "use_listener_reverb", false)
	await wait_frames(2)
	check(VA.has_reverb(plain), "turning on the emitter's use_listener_reverb while playing didn't add the leech's reverb effect")
	check(not VA.has_reverb(reverb), "turning off the emitter's use_listener_reverb while playing didn't remove the leech's reverb effect")

	await step("leech with its emitter's listener reverb", 1.0)

func add_emitter(node_name: String, use_listener_reverb: bool) -> Node:
	var node := VA.create_node(root.world, "VAEmitter")
	node.name = node_name
	node.position = root.source.position
	VA.set_value(node, "use_listener_reverb", use_listener_reverb)
	root.add_child(node)
	return node

func add_leech(node_name: String, parent: Node) -> Node:
	var node := spawn_leech(node_name, parent)
	VA.set_value(node, "looping", true)
	VA.set_value(node, "autoplay", true)
	return node

func teardown() -> void:
	plain_emitter.queue_free()
	reverb_emitter.queue_free()
	await wait_raytraced()
