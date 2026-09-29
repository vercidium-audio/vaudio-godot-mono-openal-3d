extends "res://scenarios/lib/scenario.gd"

# Shrinks the world's bounds so they end between the listener and source, leaving the source outside. A clamped source (the default) is pulled back inside and still raytraced; an unclamped one follows emitters_outside_the_world_are_muffled

var original_size
var original_muffled: bool
var original_clamp: bool

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	original_size = VA.get_value(root.world, "bounds_size")
	original_muffled = VA.get_value(root.world, "emitters_outside_the_world_are_muffled")
	original_clamp = VA.get_value(root.source, "clamp_position")
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	var inside := await measure_muffling("source inside", "source inside the world - expect clear speech")

	# The bounds start at the world's position, so this ends them at x = 0 - the listener is at negative x, the source at positive x
	var shrunk = original_size
	shrunk.x = -root.world.position.x
	VA.set_value(root.world, "bounds_size", shrunk)
	VA.set_value(root.world, "emitters_outside_the_world_are_muffled", true)

	VA.set_value(root.source, "clamp_position", true)
	var clamped := await measure_muffling("outside, clamped", "source outside the world but clamped to its edge - expect clear speech")

	VA.set_value(root.source, "clamp_position", false)
	var muffled := await measure_muffling("outside, muffled", "unclamped source outside the world, outside emitters muffled - expect near-silence")
	VA.set_value(root.world, "emitters_outside_the_world_are_muffled", false)
	var unmuffled := await measure_muffling("outside, unmuffled", "unclamped source outside the world, outside emitters unmuffled - expect clear speech")

	restore()
	var restored := await measure_muffling("bounds restored", "world bounds restored - expect clear speech")

	check(inside.y > 0.9, "muffling HF %.4f with the source inside the world, expected almost unmuffled" % inside.y)
	check(clamped.y > 0.9, "muffling HF %.4f with the source clamped to the world's edge, expected almost unmuffled" % clamped.y)
	check(muffled.x < 0.05 and muffled.y < 0.05, "muffling LF %.4f HF %.4f outside the world with emitters_outside_the_world_are_muffled on, expected fully muffled" % [muffled.x, muffled.y])
	check(unmuffled.x > 0.9 and unmuffled.y > 0.9, "muffling LF %.4f HF %.4f outside the world with emitters_outside_the_world_are_muffled off, expected unmuffled" % [unmuffled.x, unmuffled.y])
	check(absf(restored.y - inside.y) < 0.02, "muffling HF %.4f after restoring the bounds didn't return to %.4f" % [restored.y, inside.y])

func restore() -> void:
	VA.set_value(root.source, "clamp_position", original_clamp)
	VA.set_value(root.world, "emitters_outside_the_world_are_muffled", original_muffled)
	VA.set_value(root.world, "bounds_size", original_size)

func teardown() -> void:
	restore()
	await wait_raytraced()
