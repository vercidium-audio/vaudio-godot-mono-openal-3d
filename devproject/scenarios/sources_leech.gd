extends "res://scenarios/lib/scenario.gd"

# A VASourceLeech copies its parent VAEmitter's muffling. The emitter sits on the scene source, so the leech's muffling follows the source's as the listener moves behind a brick partition. A leech under a VAListener never plays

var partition: Node
var emitter: Node
var leech: Node
var listener_leech: Node
var start_position

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	start_position = root.listener.global_position
	partition = DIM.add_reverb_room_partition(root, "brick")

	emitter = VA.create_node(root.world, "VAEmitter")
	emitter.name = "LeechEmitter"
	emitter.position = root.source.position
	root.add_child(emitter)

	leech = spawn_leech("Leech", emitter)

	expect_warning("use a VASourceRelative instead")
	listener_leech = spawn_leech("ListenerLeech", root.listener)

	await wait_raytraced_by_listener(root.source)
	check(await wait_until(func(): return VA.is_ready_to_play(leech)), "leech never became ready to play")

func run() -> void:
	root.listener.global_position = DIM.REVERB_ROOM_LOS_LISTENER
	var los := await measure_muffling("line of sight", "listener beside the source - expect clear speech", leech)
	var source_los := Vector2(VA.muffling_lf(root.source), VA.muffling_hf(root.source))

	root.listener.global_position = start_position
	var behind := await measure_muffling("behind wall", "listener behind the brick wall - expect muffled speech", leech)
	var source_behind := Vector2(VA.muffling_lf(root.source), VA.muffling_hf(root.source))

	check(los.y > 0.9, "leech muffling HF %.4f with line of sight, expected almost unmuffled" % los.y)
	check(behind.y < los.y * 0.5, "leech muffling HF %.4f behind the wall isn't below half the line of sight gain %.4f" % [behind.y, los.y])
	check(los.distance_to(source_los) < 0.05, "leech muffling %s with line of sight doesn't match the source's %s" % [los, source_los])
	check(behind.distance_to(source_behind) < 0.05, "leech muffling %s behind the wall doesn't match the source's %s" % [behind, source_behind])
	check(not VA.is_ready_to_play(listener_leech), "a leech under the listener is ready to play")

func teardown() -> void:
	root.listener.global_position = start_position
	listener_leech.queue_free()
	emitter.queue_free()
	partition.queue_free()
	await wait_raytraced()

func spawn_leech(node_name: String, parent: Node) -> Node:
	var node := VA.create_node(root.world, "VASourceLeech")
	node.name = node_name
	VA.set_value(node, "streams", VA.get_value(root.source, "streams"))
	parent.add_child(node)
	return node
