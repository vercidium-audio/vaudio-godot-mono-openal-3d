extends "res://scenarios/lib/scenario.gd"

# Pins down that vaudio geometry follows tree membership and metadata only: hiding a node, disabling a collision shape or disabling processing leaves its primitive in place. Invisible occluders are common, so hiding a wall mustn't open it up acoustically - remove the node, or change its material and call sync_primitive, to take it out of the simulation

var partition: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	partition = DIM.add_reverb_room_partition(root, "brick")
	var shown := await measure_muffling("visual wall", "brick wall - expect muffled speech")
	partition.visible = false
	var hidden := await measure_muffling("visual wall hidden", "wall hidden - expect it to stay muffled")
	partition.visible = true
	partition.process_mode = Node.PROCESS_MODE_DISABLED
	var unprocessed := await measure_muffling("visual wall process_mode disabled", "wall's processing disabled - expect it to stay muffled")
	partition.queue_free()

	partition = DIM.add_reverb_room_body_partition(root, "brick", 1)
	var enabled := await measure_muffling("body wall", "static body wall - expect muffled speech")
	for collider in DIM.colliders(partition):
		collider.disabled = true
	var disabled := await measure_muffling("body wall collider disabled", "wall's collision shape disabled - expect it to stay muffled")
	partition.queue_free()
	partition = null
	var open := await measure_muffling("no wall", "no wall - expect clear speech")

	check(shown.y < open.y * 0.5, "muffling HF %.4f with the brick wall isn't below half the open room's %.4f, so the hide/process checks prove nothing" % [shown.y, open.y])
	check(enabled.y < open.y * 0.5, "muffling HF %.4f with the static body wall isn't below half the open room's %.4f, so the disabled check proves nothing" % [enabled.y, open.y])
	check(absf(hidden.y - shown.y) < 0.02, "hiding the wall changed muffling HF from %.4f to %.4f" % [shown.y, hidden.y])
	check(absf(unprocessed.y - shown.y) < 0.02, "disabling the wall's processing changed muffling HF from %.4f to %.4f" % [shown.y, unprocessed.y])
	check(absf(disabled.y - enabled.y) < 0.02, "disabling the wall's collision shape changed muffling HF from %.4f to %.4f" % [enabled.y, disabled.y])

func teardown() -> void:
	if partition:
		partition.queue_free()
	await wait_raytraced()
