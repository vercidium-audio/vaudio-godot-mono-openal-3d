extends "res://scenarios/lib/scenario.gd"

# Adds a sealed wall of 20 StaticBodies at runtime, then frees it. The material is only on the pieces' shared parent, so this also covers colliders inheriting a material when a whole subtree enters the tree at once (e.g. an instanced scene)

var partition: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	var baseline := await measure_muffling("no wall", "no wall - expect clear speech")
	partition = DIM.add_reverb_room_body_partition(root, "brick")
	var walled := await measure_muffling("20-body wall", "wall of 20 static bodies added - expect muffled speech")
	partition.queue_free()
	partition = null
	var freed := await measure_muffling("wall freed", "wall freed - expect clear speech again")

	check(walled.y < baseline.y * 0.5, "muffling HF %.4f with the 20-body wall isn't below half the open room's %.4f" % [walled.y, baseline.y])
	check(absf(freed.y - baseline.y) < 0.02 and absf(freed.x - baseline.x) < 0.02, "muffling LF %.4f HF %.4f after freeing the wall didn't return to the open room's LF %.4f HF %.4f" % [freed.x, freed.y, baseline.x, baseline.y])

func teardown() -> void:
	if partition:
		partition.queue_free()
	await wait_raytraced()
