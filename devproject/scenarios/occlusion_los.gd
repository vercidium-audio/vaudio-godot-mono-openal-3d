extends "res://scenarios/lib/scenario.gd"

# Splits the reverb room with a sealed brick partition and moves the listener between the source's side (line of sight) and the far side (behind the wall). test_scene.tscn isn't used because in 3D enough energy goes over and around its brick wall to saturate the occlusion energy cap

var partition: Node
var start_position

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	start_position = root.listener.global_position
	partition = DIM.add_reverb_room_partition(root, "brick")
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	root.listener.global_position = DIM.REVERB_ROOM_LOS_LISTENER
	var los := await measure_muffling("line of sight", "listener beside the source - expect clear speech")
	root.listener.global_position = start_position
	var behind := await measure_muffling("behind wall", "listener behind the brick wall - expect muffled speech")
	root.listener.global_position = DIM.REVERB_ROOM_LOS_LISTENER
	var back := await measure_muffling("line of sight again", "listener back beside the source - expect clear speech again")

	check(los.y > 0.9, "muffling HF %.4f with line of sight, expected almost unmuffled" % los.y)
	check(behind.y < los.y * 0.5, "muffling HF %.4f behind the wall isn't below half the line of sight gain %.4f" % [behind.y, los.y])
	check(absf(back.y - los.y) < 0.02, "muffling HF %.4f after returning didn't recover to the line of sight gain %.4f" % [back.y, los.y])

func teardown() -> void:
	root.listener.global_position = start_position
	partition.queue_free()
	await wait_raytraced()
