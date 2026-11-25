# AI-created

extends "res://scenarios/lib/scenario.gd"

# FMOD's 3D rolloff comes after a voice's reverb send, so the send applies the voice's rolloff curve itself: a leech far from the listener sends less to the listener's reverb than one beside it, and moving only the listener swaps them

var near_emitter: Node
var far_emitter: Node
var near: Node
var far: Node
var original_listener_position

func _init() -> void:
	scene = REVERB_ROOM
	requires_backends = ["fmod"]
	backend_smoke = true

func setup() -> void:
	original_listener_position = root.listener.global_position
	near_emitter = add_emitter("NearEmitter", root.listener.global_position)
	far_emitter = add_emitter("FarEmitter", root.source.global_position)
	near = add_leech("Near", near_emitter)
	far = add_leech("Far", far_emitter)

	check(await wait_playing(near), "the near leech never started")
	check(await wait_playing(far), "the far leech never started")
	check(await wait_until(func(): return send_level(near) >= 0 and send_level(far) >= 0), "the leeches never sent to the listener's reverb")

func run() -> void:
	await wait_frames(2)
	var near_level := send_level(near)
	var far_level := send_level(far)
	check(far_level < near_level, "the far leech's reverb send (%f) isn't quieter than the near one's (%f)" % [far_level, near_level])
	await step("reverb from a near and a far source", 1.0)

	# Only the listener moves, so the sources' own positions don't change
	root.listener.global_position = root.source.global_position
	await wait_frames(3)
	var moved_near := send_level(near)
	var moved_far := send_level(far)
	check(moved_far > far_level, "the far leech's reverb send (%f -> %f) didn't get louder when the listener moved to it" % [far_level, moved_far])
	check(moved_near < moved_far, "after the listener moved, the now-far leech's reverb send (%f) isn't quieter than the now-near one's (%f)" % [moved_near, moved_far])
	await step("listener moved to the far source", 1.0)

func send_level(player: Node) -> float:
	return VA.reverb_send_level(root.world, player)

func add_emitter(node_name: String, position) -> Node:
	var node := VA.create_node(root.world, "VAEmitter")
	node.name = node_name
	VA.set_value(node, "use_listener_reverb", true)
	root.add_child(node)
	node.global_position = position
	return node

func add_leech(node_name: String, parent: Node) -> Node:
	var node := spawn_leech(node_name, parent)
	VA.set_value(node, "looping", true)
	VA.set_value(node, "autoplay", true)
	# The default (8) is wider than the 3D reverb room, and nothing attenuates inside it
	VA.set_value(node, "reference_distance", 1.0)
	return node

func teardown() -> void:
	root.listener.global_position = original_listener_position
	near_emitter.queue_free()
	far_emitter.queue_free()
	await wait_raytraced()
