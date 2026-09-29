extends "res://scenarios/lib/scenario.gd"

# Tweens a sealed brick partition from beyond the source to between the listener and source and back, so the primitive follows a transform that changes every frame

var partition: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	partition = DIM.add_reverb_room_partition(root, "brick")
	partition.position.x = DIM.REVERB_ROOM_BEYOND_SOURCE_X
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	var beyond := await measure_muffling("wall beyond source", "wall beyond the source - expect clear speech")
	await slide(0.0, "wall sliding in between the listener and source - expect the muffling to sweep in")
	var between := await measure_muffling("wall between", "wall between the listener and source - expect muffled speech")
	await slide(DIM.REVERB_ROOM_BEYOND_SOURCE_X, "wall sliding back out - expect the muffling to sweep out")
	var back := await measure_muffling("wall beyond source again", "wall beyond the source again - expect clear speech")

	check(beyond.y > 0.9, "muffling HF %.4f with the wall beyond the source, expected almost unmuffled" % beyond.y)
	check(between.y < beyond.y * 0.5, "muffling HF %.4f with the wall moved between isn't below half the unblocked gain %.4f" % [between.y, beyond.y])
	check(absf(back.y - beyond.y) < 0.02, "muffling HF %.4f after moving the wall back didn't recover to %.4f" % [back.y, beyond.y])

func slide(x: float, banner: String) -> void:
	if listen:
		print(">> %s" % banner)
	var tween := root.create_tween()
	tween.tween_property(partition, "position:x", x, duration(1.0))
	await tween.finished

func teardown() -> void:
	partition.queue_free()
	await wait_raytraced()
