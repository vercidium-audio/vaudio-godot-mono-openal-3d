extends "res://scenarios/lib/scenario.gd"

# VAVisualisation's random_visualisation_seed / visualisation_seed. With a fixed seed every update casts the same rays, so each batch of diamonds lands in the same places. With a random seed they don't. Batches are told apart by the spawn time each diamond stores in its custom data

const UPDATE_FREQUENCY_MS := 100
const RENDER_TIMEOUT_MS := 5000

var visualisation: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	# Inspector visibility follows random_visualisation_seed
	visualisation = VA.create_node(root.world, "VAVisualisation")
	check(VA.get_value(visualisation, "random_visualisation_seed") == true, "random_visualisation_seed should default to true")
	check(not VA.is_in_inspector(visualisation, "visualisation_seed"), "visualisation_seed shouldn't be in the inspector while random_visualisation_seed is true")
	VA.set_value(visualisation, "random_visualisation_seed", false)
	check(VA.is_in_inspector(visualisation, "visualisation_seed"), "visualisation_seed should be in the inspector once random_visualisation_seed is false")
	VA.set_value(visualisation, "visualisation_seed", 42)
	check(VA.get_value(visualisation, "visualisation_seed") == 42, "visualisation_seed didn't round-trip")

	# A fixed seed applied when the visualisation starts
	add_visualisation()
	var batches := await wait_batches(2, 0.0)
	check(batches.size() >= 2 and batches[0] == batches[1], "batches with a fixed seed should land in the same places")
	visualisation.free()

	# A random seed
	visualisation = VA.create_node(root.world, "VAVisualisation")
	add_visualisation()
	batches = await wait_batches(2, 0.0)
	check(batches.size() >= 2 and batches[0] != batches[1], "batches with a random seed shouldn't land in the same places")

	# Switching to a fixed seed while it's running
	VA.set_value(visualisation, "visualisation_seed", 7)
	VA.set_value(visualisation, "random_visualisation_seed", false)
	batches = await wait_batches(2, now() + UPDATE_FREQUENCY_MS / 1000.0)
	check(batches.size() >= 2 and batches[0] == batches[1], "batches should land in the same places after switching to a fixed seed at runtime")

func add_visualisation() -> void:
	VA.set_value(visualisation, "update_frequency_ms", UPDATE_FREQUENCY_MS)
	VA.set_value(visualisation, "ray_count", 8)
	VA.set_value(visualisation, "bounce_count", 2)
	VA.set_value(visualisation, "max_distance", 0.0)
	root.source.add_child(visualisation)

func now() -> float:
	return Time.get_ticks_msec() / 1000.0

func multimesh() -> MultiMesh:
	for child in visualisation.get_children():
		if "multimesh" in child and child.multimesh != null:
			return child.multimesh
	return null

# Each batch's sorted diamond positions, oldest first, skipping batches spawned before `after` (the update in flight when a property changed can still use the old values)
func read_batches(after: float) -> Array:
	var mesh := multimesh()
	if mesh == null:
		return []

	var by_time := {}
	for i in mesh.visible_instance_count:
		var time := mesh.get_instance_custom_data(i).r
		if time <= after:
			continue
		var origin = mesh.get_instance_transform_2d(i).origin if mesh.transform_format == MultiMesh.TRANSFORM_2D else mesh.get_instance_transform(i).origin
		if not by_time.has(time):
			by_time[time] = []
		by_time[time].append(origin)

	var times := by_time.keys()
	times.sort()
	var batches := []
	for time in times:
		var origins: Array = by_time[time]
		origins.sort()
		batches.append(origins)
	return batches

func wait_batches(count: int, after: float) -> Array:
	var started := Time.get_ticks_msec()
	while read_batches(after).size() < count and Time.get_ticks_msec() - started < RENDER_TIMEOUT_MS:
		await root.get_tree().process_frame
	return read_batches(after)

func teardown() -> void:
	if is_instance_valid(visualisation):
		visualisation.free()
