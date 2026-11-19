extends "res://scenarios/lib/scenario.gd"

# VAVisualisation's random_visualisation_seed / visualisation_seed. With a fixed seed every update casts the same rays, so each batch lands in the same places. With a random seed they don't. Batches come from the visualisation_data_received signal, since headless Godot's dummy renderer doesn't keep MultiMesh instance data

const UPDATE_FREQUENCY_MS := 100
const RENDER_TIMEOUT_MS := 5000

var visualisation: Node
var batches: Array = []

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
	var received := await wait_batches(2)
	check(received.size() >= 2 and received[0] == received[1], "batches with a fixed seed should land in the same places")
	visualisation.free()

	# A random seed
	visualisation = VA.create_node(root.world, "VAVisualisation")
	add_visualisation()
	received = await wait_batches(2)
	check(received.size() >= 2 and received[0] != received[1], "batches with a random seed shouldn't land in the same places")

	# Switching to a fixed seed while it's running. The update in flight when the seed changes can still use the old one, so skip a batch
	VA.set_value(visualisation, "visualisation_seed", 7)
	VA.set_value(visualisation, "random_visualisation_seed", false)
	received = await wait_batches(3)
	check(received.size() >= 3 and received[1] == received[2], "batches should land in the same places after switching to a fixed seed at runtime")

func add_visualisation() -> void:
	VA.set_value(visualisation, "update_frequency_ms", UPDATE_FREQUENCY_MS)
	VA.set_value(visualisation, "ray_count", 8)
	VA.set_value(visualisation, "bounce_count", 2)
	VA.set_value(visualisation, "max_distance", 0.0)
	VA.connect_signal(visualisation, "visualisation_data_received", on_visualisation_data_received)
	root.source.add_child(visualisation)

func on_visualisation_data_received(positions, normals) -> void:
	check(positions.size() > 0, "visualisation_data_received should have positions")
	check(positions.size() == normals.size(), "visualisation_data_received should have one normal per position")
	var sorted := Array(positions)
	sorted.sort()
	batches.append(sorted)

# Each batch's sorted positions, oldest first, counting from when this was called
func wait_batches(count: int) -> Array:
	batches = []
	var started := Time.get_ticks_msec()
	while batches.size() < count and Time.get_ticks_msec() - started < RENDER_TIMEOUT_MS:
		await root.get_tree().process_frame
	return batches.duplicate()

func teardown() -> void:
	if is_instance_valid(visualisation):
		visualisation.free()
