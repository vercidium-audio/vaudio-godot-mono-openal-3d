extends "res://scenarios/lib/scenario.gd"

# Changes VAWorld.collision_layers (which rebuilds every primitive) and moves the walls in the same frame. The rebuild queue_free()s each node's old transform watcher, which stays in the tree until the end of the frame, so the move's transform notification reaches it after its primitive was destroyed. Any error or crash fails it

const ROUNDS := 32

# A layer nothing in reverb_room.tscn uses, so toggling it rebuilds the primitives without changing which ones are included
const SPARE_LAYER := 1 << 19

var mesh_partition: Node
var body_partition: Node
var original_layers: int

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	original_layers = VA.get_value(root.world, "collision_layers")
	mesh_partition = DIM.add_reverb_room_partition(root, "brick")
	body_partition = DIM.add_reverb_room_body_partition(root, "brick")
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	var before := await measure_muffling("before rounds", "walls between the listener and source - expect muffled speech")

	for round in ROUNDS:
		# Resumes from process_frame, which Godot emits just before it flushes transform notifications and long before it deletes queue_free()d nodes
		await root.get_tree().process_frame
		VA.set_value(root.world, "collision_layers", original_layers ^ SPARE_LAYER if round % 2 == 0 else original_layers)
		var offset := 0.1 if round % 2 == 0 else 0.0
		mesh_partition.position.x = offset
		body_partition.position.x = offset

	await wait_frames(2)
	if listen:
		print(">> %d rounds of rebuilding while moving" % ROUNDS)

	var after := await measure_muffling("after rounds", "walls still between the listener and source - expect muffled speech")

	# Compared to a rebuild with nothing moving rather than to before the rounds, since a rebuild re-adds the primitives in a different order, which changes how much leaks through the seams between the pieces in 3D (0.14 vs 0.39)
	VA.set_value(root.world, "collision_layers", original_layers ^ SPARE_LAYER)
	VA.set_value(root.world, "collision_layers", original_layers)
	var rebuilt := await measure_muffling("rebuilt in place", "walls rebuilt in place - expect muffled speech")

	check(before.y < 0.5, "muffling HF %.4f before the rounds, expected the walls to muffle" % before.y)
	check(absf(after.y - rebuilt.y) < 0.02 and absf(after.x - rebuilt.x) < 0.02, "muffling LF %.4f HF %.4f after the rounds didn't match LF %.4f HF %.4f after rebuilding in place, so a primitive was left at a stale transform" % [after.x, after.y, rebuilt.x, rebuilt.y])

func teardown() -> void:
	VA.set_value(root.world, "collision_layers", original_layers)
	mesh_partition.queue_free()
	body_partition.queue_free()
	await wait_raytraced()
