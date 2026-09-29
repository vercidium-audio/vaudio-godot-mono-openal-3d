extends "res://scenarios/lib/scenario.gd"

# Rotates and scales a sealed brick partition so it starts and stops blocking the listener. It's long enough to seal the room either way round, so a quarter turn moves it from between the listener and source to beside them, and squashing its length leaves a narrow column sound goes around

var partition: Node
var start_position

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	start_position = root.listener.global_position
	root.listener.global_position = DIM.REVERB_ROOM_SAME_SIDE_LISTENER
	partition = DIM.add_reverb_room_long_partition(root, "brick")
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	var across := await measure_muffling("across", "partition between the listener and source - expect muffled speech")
	DIM.turn(partition, 1)
	var turned := await measure_muffling("turned", "partition turned a quarter to run beside them - expect clear speech")
	DIM.turn(partition, 0)
	var turned_back := await measure_muffling("turned back", "partition turned back - expect muffled speech")
	DIM.squash(partition, 0.05)
	var squashed := await measure_muffling("squashed", "partition squashed into a narrow column - expect clear speech")
	DIM.squash(partition, 1.0)
	var unsquashed := await measure_muffling("unsquashed", "partition scaled back - expect muffled speech")

	check(turned.y > 0.9, "muffling HF %.4f with the partition turned beside them, expected almost unmuffled" % turned.y)
	check(across.y < turned.y * 0.5, "muffling HF %.4f with the partition across isn't below half the turned gain %.4f" % [across.y, turned.y])
	check(absf(turned_back.y - across.y) < 0.02, "muffling HF %.4f after turning back didn't return to %.4f" % [turned_back.y, across.y])
	check(squashed.y > 0.9, "muffling HF %.4f with the partition squashed into a column, expected almost unmuffled" % squashed.y)
	check(absf(unsquashed.y - across.y) < 0.02, "muffling HF %.4f after unsquashing didn't return to %.4f" % [unsquashed.y, across.y])

func teardown() -> void:
	root.listener.global_position = start_position
	partition.queue_free()
	await wait_raytraced()
