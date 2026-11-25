# AI-created

extends "res://scenarios/lib/scenario.gd"

# VAWorld's audio backend settings (master volume, distance model, doppler scale, meters per unit, speed of sound, reverb only) reach the backend: the scene's saved values once the world is in the tree, and changes straight away. Mono only until the native plugin has doppler_scale and the backend interface

const PROPERTIES := ["master_volume", "distance_model", "doppler_scale", "meters_per_unit", "speed_of_sound", "reverb_only"]

var original := {}

func _init() -> void:
	backend_smoke = true

func run() -> void:
	var world: Node = root.world
	for property in PROPERTIES:
		original[property] = VA.get_value(world, property)

	check(VA.is_in_inspector(world, "doppler_scale"), "doppler_scale isn't shown in the inspector")
	check_backend_matches(world, "on load")

	VA.set_value(world, "doppler_scale", 2.5)
	VA.set_value(world, "distance_model", 3)
	VA.set_value(world, "master_volume", 0.5)
	VA.set_value(world, "meters_per_unit", 0.25)
	VA.set_value(world, "speed_of_sound", 300.0)
	check_backend_matches(world, "after setting them")
	check(is_equal_approx(VA.backend_settings(world)["doppler_scale"], 2.5), "the backend's doppler scale isn't 2.5")

	VA.set_value(world, "doppler_scale", -1.0)
	check(VA.get_value(world, "doppler_scale") == 0.0, "doppler_scale isn't clamped to 0")
	check_backend_matches(world, "after a negative doppler scale")

func check_backend_matches(world: Node, when: String) -> void:
	var backend := VA.backend_settings(world)
	for property in PROPERTIES:
		var expected = VA.get_value(world, property)
		var actual = backend.get(property)
		var matches: bool = is_equal_approx(actual, expected) if expected is float else actual == expected
		check(matches, "%s: the backend's %s is %s, but the world's is %s" % [when, property, actual, expected])

func teardown() -> void:
	for property in original:
		VA.set_value(root.world, property, original[property])
