extends "res://scenarios/lib/scenario.gd"

# Adds a second VAListener beside the source, with a sealed brick partition between it and the scene's listener, then hands the current listener back and forth via make_current(), current = false, removing or freeing the current listener, and adding a listener with current already enabled. The source's muffling has to follow whichever listener is current, and exactly one listener is current at a time

# Copied from the scene's listener so every listener raytraces the same way, since the shared SDK emitter takes on the current listener's settings
const COPIED_PROPERTIES := ["occlusion_ray_count", "occlusion_bounce_count", "permeation_ray_count", "permeation_bounce_count", "scattering_seed"]
const TOLERANCE := 0.05

var partition: Node
var second: Node
var third: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	partition = DIM.add_reverb_room_partition(root, "brick")
	second = add_listener("SecondListener", false)
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	check_current(root.listener, "after adding a second listener with current disabled")
	var behind := await measure_muffling("scene listener current", "scene listener behind the wall is current - expect muffled speech")

	VA.make_current(second)
	check_current(second, "after make_current() on the second listener")
	var beside := await measure_muffling("second listener current", "listener beside the source is current - expect clear speech")

	VA.set_value(second, "current", false)
	check_current(root.listener, "after disabling current on the second listener")
	var handed_back := await measure_muffling("current disabled on second listener", "scene listener current again - expect muffled speech")

	VA.set_value(second, "current", true)
	check_current(second, "after enabling current on the second listener")
	root.remove_child(second)
	check_current(root.listener, "after removing the current second listener from the tree")
	var removed := await measure_muffling("current listener removed", "current listener removed from the tree - expect muffled speech")

	# Handing over cleared its current flag, so it shouldn't take over again when added back
	root.add_child(second)
	check_current(root.listener, "after adding the second listener back to the tree")

	expect_warning("so it replaced")
	third = add_listener("ThirdListener", true)
	check_current(third, "after adding a third listener with current enabled")
	var took_over := await measure_muffling("third listener took over", "listener added with current enabled beside the source - expect clear speech")

	third.queue_free()
	await wait_frames(2)
	third = null
	check_current(root.listener, "after freeing the current third listener")
	var freed := await measure_muffling("current listener freed", "current listener freed - expect muffled speech")

	second.queue_free()
	await wait_frames(2)
	second = null

	expect_warning("is the only listener in this world")
	VA.set_value(root.listener, "current", false)
	check_current(root.listener, "after disabling current on the only listener")
	var only := await measure_muffling("current disabled on only listener", "only listener stays current - expect muffled speech")

	# Last listener leaves, so the shared SDK emitter goes with it and is recreated when it comes back
	root.remove_child(root.listener)
	await wait_raytraced()
	root.add_child(root.listener)
	check_current(root.listener, "after removing and re-adding the only listener")
	var reAdded := await measure_muffling("only listener re-added", "only listener removed and added back - expect muffled speech")
	check(VA.is_raytraced_by_listener(root.source), "source isn't raytraced after the only listener was removed and added back")

	check(behind.y < 0.5, "muffling HF %.4f with the scene listener behind the wall, expected the partition to muffle it" % behind.y)
	check(beside.y > 0.9, "muffling HF %.4f after make_current() on the listener beside the source, expected almost unmuffled" % beside.y)
	check(took_over.y > 0.9, "muffling HF %.4f after a listener beside the source took over on being added, expected almost unmuffled" % took_over.y)
	check_same(handed_back, behind, "after disabling current on the second listener")
	check_same(removed, behind, "after removing the current listener from the tree")
	check_same(freed, behind, "after freeing the current listener")
	check_same(only, behind, "after disabling current on the only listener")
	check_same(reAdded, behind, "after removing and re-adding the only listener")

func add_listener(listener_name: String, current: bool) -> Node:
	var listener := VA.create_node(root.world, "VAListener")
	listener.name = listener_name
	listener.position = DIM.REVERB_ROOM_LOS_LISTENER
	for property in COPIED_PROPERTIES:
		VA.set_value(listener, property, VA.get_value(root.listener, property))
	VA.set_value(listener, "current", current)
	root.add_child(listener)
	return listener

# Every listener still in the tree must agree that expected is the only current one
func check_current(expected: Node, label: String) -> void:
	for listener in [root.listener, second, third]:
		if listener == null or not listener.is_inside_tree():
			continue
		var current: bool = VA.get_value(listener, "current")
		check(current == (listener == expected), "%s has current %s %s, expected %s to be the only current listener" % [listener.name, current, label, expected.name])

func check_same(muffling: Vector2, reference: Vector2, label: String) -> void:
	check(absf(muffling.y - reference.y) < TOLERANCE, "muffling HF %.4f %s differs from %.4f with the scene listener current at the start" % [muffling.y, label, reference.y])

func teardown() -> void:
	for listener in [second, third]:
		if is_instance_valid(listener):
			listener.free()
	if not root.listener.is_inside_tree():
		root.add_child(root.listener)
	VA.make_current(root.listener)
	partition.queue_free()
	await wait_raytraced()
