extends "res://scenarios/lib/scenario.gd"

# Shrinks the world's bounds so they end between the listener and source, leaving the source outside. A clamped source is pulled back inside and still raytraced; an unclamped one follows emitters_outside_the_world_are_muffled, which also stops it while on (see sources_world_bounds.gd)

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
	check(VA.is_playing(root.source), "clamped source outside the world stopped playing")

	# With emitters_outside_the_world_are_muffled on, the bounds also stop the (looping) source until it's back inside
	VA.set_value(root.source, "clamp_position", false)
	var muffled := await measure_muffling("outside, muffled", "unclamped source outside the world, outside emitters muffled - expect silence")
	check(VA.is_silenced_by_bounds(root.source), "unclamped source outside the world isn't silenced by the bounds")
	check(not VA.is_playing(root.source), "unclamped source outside the world is still playing with emitters_outside_the_world_are_muffled on")

	# With it off, the source plays clear, with no reverb until it's back inside
	VA.set_value(root.world, "emitters_outside_the_world_are_muffled", false)
	var unmuffled := await measure_muffling("outside, unmuffled", "unclamped source outside the world, outside emitters unmuffled - expect clear speech with no reverb")
	check(not VA.is_silenced_by_bounds(root.source), "source is silenced by the bounds with emitters_outside_the_world_are_muffled off")
	check(VA.is_playing(root.source), "unclamped source outside the world didn't resume with emitters_outside_the_world_are_muffled off")
	check(not VA.has_reverb(root.source), "unclamped source outside the world still has a reverb effect")

	restore()
	var restored := await measure_muffling("bounds restored", "world bounds restored - expect clear speech")
	check(VA.is_playing(root.source), "source isn't playing after restoring the bounds")
	check(VA.has_reverb(root.source), "source has no reverb effect after restoring the bounds")

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
