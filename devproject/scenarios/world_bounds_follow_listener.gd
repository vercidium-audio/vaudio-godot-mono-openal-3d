extends "res://scenarios/lib/scenario.gd"

# With bounds_follow set to Listener the bounds are centred on the current listener instead of starting at the VAWorld's position. They recentre once the listener is bounds_update_distance from the box's centre, and straight away when bounds_size or the current listener changes, and moving the VAWorld itself does nothing. Shrinking the bounds so the source falls outside them muffles it until the listener walks close enough to bring it back inside. While following, bounds_size is clamped to at least twice bounds_update_distance on every axis

const FOLLOW_NONE := 0
const FOLLOW_LISTENER := 1
const UPDATE_DISTANCE: float = DIM.BOUNDS_UPDATE_DISTANCE

# Copied from the scene's listener since the shared SDK emitter takes on the current listener's settings
const COPIED_PROPERTIES := ["occlusion_ray_count", "occlusion_bounce_count", "permeation_ray_count", "permeation_bounce_count", "scattering_seed"]

var original_position
var original_size
var original_distance: float
var original_muffled: bool
var original_clamp: bool
var original_listener_position
var second: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	original_position = root.world.position
	original_size = VA.get_value(root.world, "bounds_size")
	original_distance = VA.get_value(root.world, "bounds_update_distance")
	original_muffled = VA.get_value(root.world, "emitters_outside_the_world_are_muffled")
	original_clamp = VA.get_value(root.source, "clamp_position")
	original_listener_position = root.listener.global_position
	VA.set_value(root.world, "emitters_outside_the_world_are_muffled", true)
	VA.set_value(root.source, "clamp_position", false)
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	check(VA.get_value(root.world, "bounds_follow") == FOLLOW_NONE, "bounds_follow doesn't default to None")
	check_bounds(original_position, "before following")

	VA.set_value(root.world, "bounds_update_distance", UPDATE_DISTANCE)
	VA.set_value(root.world, "bounds_follow", FOLLOW_LISTENER)
	check_size(original_size, "after enabling bounds_follow with bounds_size already over twice bounds_update_distance")
	check_bounds(centred_on(original_listener_position, original_size), "straight after enabling bounds_follow")
	var inside := await measure_muffling("source inside", "source inside the listener's bounds - expect clear speech")

	# The VAWorld's own position no longer matters
	root.world.position = original_position + offset(50, 0)
	await wait_frames(2)
	check_bounds(centred_on(original_listener_position, original_size), "after moving the VAWorld")

	# The bounds only follow once the listener is bounds_update_distance from their centre
	await move_listener(root.listener, original_listener_position + offset(0.6, 0))
	await move_listener(root.listener, original_listener_position + offset(0.6, 0.7))
	check_bounds(centred_on(original_listener_position, original_size), "after the listener moved less than bounds_update_distance")

	var stepped = original_listener_position + offset(0.6, 0.9)
	await move_listener(root.listener, stepped)
	check_bounds(centred_on(stepped, original_size), "after the listener moved bounds_update_distance")

	# Resizing stays centred on the listener even though it has moved less than bounds_update_distance. The bounds now end 3.5 bounds_update_distances short of the source
	var nearby = stepped + offset(-0.6, 0)
	await move_listener(root.listener, nearby)
	check_bounds(centred_on(stepped, original_size), "before resizing the bounds")

	var shrunk = original_size
	shrunk.x = UPDATE_DISTANCE * 5.0
	VA.set_value(root.world, "bounds_size", shrunk)
	check_bounds(centred_on(nearby, shrunk), "straight after resizing the bounds")
	var outside := await measure_muffling("source outside", "source outside the shrunk bounds - expect near-silence")

	# Walking towards the source brings it back inside
	var close = root.source.global_position - offset(1, 0)
	await move_listener(root.listener, close)
	check_bounds(centred_on(close, shrunk), "after the listener walked to the source")
	var followed := await measure_muffling("source inside again", "source inside the bounds that followed the listener - expect clear speech")

	# A new current listener is centred straight away, however close it is
	VA.set_value(root.world, "bounds_size", original_size)
	var second_position = close - offset(0.5, 0)
	second = VA.create_node(root.world, "VAListener")
	second.name = "SecondListener"
	second.position = second_position
	for property in COPIED_PROPERTIES:
		VA.set_value(second, property, VA.get_value(root.listener, property))
	VA.set_value(second, "current", false)
	root.add_child(second)
	await wait_frames(2)
	check_bounds(centred_on(close, original_size), "after adding a listener that isn't current")

	VA.make_current(second)
	await wait_frames(2)
	check_bounds(centred_on(second_position, original_size), "after make_current() on the second listener")

	root.remove_child(second)
	await wait_frames(2)
	check_bounds(centred_on(close, original_size), "after the current listener left and handed over")

	# With no listener left the bounds stay where they were, then centre on the next one to arrive
	root.remove_child(root.listener)
	await wait_frames(2)
	check_bounds(centred_on(close, original_size), "after removing every listener")

	root.listener.position = original_listener_position
	root.add_child(root.listener)
	await wait_frames(2)
	check_bounds(centred_on(original_listener_position, original_size), "after adding the listener back")

	# The listener can walk just under bounds_update_distance from the centre before the bounds recentre, so bounds_size is clamped to at least twice it on every axis, and stays centred
	var thin = original_size
	thin.y = UPDATE_DISTANCE
	VA.set_value(root.world, "bounds_size", thin)
	check_size(thin.maxf(UPDATE_DISTANCE * 2.0), "after setting a bounds_size side under twice bounds_update_distance")

	var widest: float = original_size[original_size.max_axis_index()]
	VA.set_value(root.world, "bounds_update_distance", widest)
	var grown = original_size.maxf(widest * 2.0)
	check_size(grown, "after raising bounds_update_distance past half of every bounds_size side")
	check_bounds(centred_on(original_listener_position, grown), "after bounds_update_distance grew bounds_size")

	# Lowering bounds_update_distance again leaves bounds_size alone
	VA.set_value(root.world, "bounds_update_distance", UPDATE_DISTANCE)
	check_size(grown, "after lowering bounds_update_distance")
	VA.set_value(root.world, "bounds_size", original_size)

	# Back to a fixed box at the VAWorld's position, which moves with it straight away whatever bounds_update_distance is
	root.world.position = original_position
	VA.set_value(root.world, "bounds_follow", FOLLOW_NONE)
	check_bounds(original_position, "after setting bounds_follow to None")

	# A fixed box isn't clamped
	VA.set_value(root.world, "bounds_size", thin)
	check_size(thin, "after setting a thin bounds_size with bounds_follow None")
	VA.set_value(root.world, "bounds_size", original_size)

	root.world.position = original_position + offset(0.1, 0)
	await wait_frames(2)
	check_bounds(original_position + offset(0.1, 0), "after moving the VAWorld with bounds_follow None")

	root.world.position = original_position
	await wait_frames(2)
	var fixed := await measure_muffling("fixed bounds", "back to the fixed bounds - expect clear speech")

	check(inside.y > 0.9, "muffling HF %.4f inside the listener's bounds, expected almost unmuffled" % inside.y)
	check(outside.x < 0.05 and outside.y < 0.05, "muffling LF %.4f HF %.4f outside the shrunk bounds, expected fully muffled" % [outside.x, outside.y])
	check(followed.y > 0.9, "muffling HF %.4f inside the bounds that followed the listener, expected almost unmuffled" % followed.y)
	check(fixed.y > 0.9, "muffling HF %.4f after going back to the fixed bounds, expected almost unmuffled" % fixed.y)

# Offset in bounds_update_distances
func offset(x: float, y: float):
	return DIM.planar(x, y) * UPDATE_DISTANCE

func centred_on(center, size):
	return center - size * 0.5

func check_bounds(expected, label: String) -> void:
	var actual = VA.bounds_position(root.world)
	check(actual.is_equal_approx(expected), "bounds position %s %s, expected %s" % [actual, label, expected])

func check_size(expected, label: String) -> void:
	var actual = VA.get_value(root.world, "bounds_size")
	check(actual.is_equal_approx(expected), "bounds_size %s %s, expected %s" % [actual, label, expected])

func move_listener(listener: Node, position) -> void:
	listener.global_position = position
	await wait_frames(2)

func teardown() -> void:
	if is_instance_valid(second):
		second.free()
	if not root.listener.is_inside_tree():
		root.add_child(root.listener)
	root.listener.global_position = original_listener_position
	VA.make_current(root.listener)
	VA.set_value(root.world, "bounds_follow", FOLLOW_NONE)
	VA.set_value(root.world, "bounds_update_distance", original_distance)
	VA.set_value(root.world, "bounds_size", original_size)
	root.world.position = original_position
	VA.set_value(root.world, "emitters_outside_the_world_are_muffled", original_muffled)
	VA.set_value(root.source, "clamp_position", original_clamp)
	await wait_raytraced()
