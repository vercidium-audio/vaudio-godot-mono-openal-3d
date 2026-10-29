extends "res://scenarios/lib/scenario.gd"

# Every listener shares one SDK emitter, so a VAVisualisation under a listener must only render while that listener is current, and must hand over to the new current listener's visualisation. Each visualisation's multimesh visible instance count shows whether it's rendering - it drops to 0 the moment the visualisation stops

const COPIED_PROPERTIES := ["occlusion_ray_count", "occlusion_bounce_count", "permeation_ray_count", "permeation_bounce_count", "scattering_seed"]

# Visualisation updates to sit through when checking a stopped visualisation stays stopped
const QUIET_UPDATES := 5
const UPDATE_FREQUENCY_MS := 100
const RENDER_TIMEOUT_MS := 5000

var second: Node
var scene_visualisation: Node
var second_visualisation: Node
var source_visualisation: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	second = VA.create_node(root.world, "VAListener")
	second.name = "SecondListener"
	second.position = DIM.REVERB_ROOM_LOS_LISTENER
	for property in COPIED_PROPERTIES:
		VA.set_value(second, property, VA.get_value(root.listener, property))
	VA.set_value(second, "current", false)
	root.add_child(second)

	scene_visualisation = add_visualisation(root.listener)
	second_visualisation = add_visualisation(second)
	source_visualisation = add_visualisation(root.source)
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	check(await wait_rendering(source_visualisation), "source visualisation never rendered")
	check(await wait_rendering(scene_visualisation), "scene listener's visualisation never rendered while it was current")
	await check_stays_stopped(second_visualisation, "second listener's visualisation rendered while it wasn't current")

	VA.make_current(second)
	check(rendered(scene_visualisation) == 0, "scene listener's visualisation still showing after make_current() on the second listener")
	check(await wait_rendering(second_visualisation), "second listener's visualisation never rendered after make_current()")
	await check_stays_stopped(scene_visualisation, "scene listener's visualisation rendered after the second listener became current")

	VA.set_value(second, "current", false)
	check(rendered(second_visualisation) == 0, "second listener's visualisation still showing after disabling current")
	check(await wait_rendering(scene_visualisation), "scene listener's visualisation never rendered after becoming current again")
	await check_stays_stopped(second_visualisation, "second listener's visualisation rendered after disabling current")

	# Leaving and re-entering the tree must restart it
	root.listener.remove_child(scene_visualisation)
	check(rendered(scene_visualisation) == 0, "scene listener's visualisation still showing after leaving the tree")
	root.listener.add_child(scene_visualisation)
	check(await wait_rendering(scene_visualisation), "scene listener's visualisation never rendered after re-entering the tree")

	# Freeing the current listener hands the emitter back, so the scene listener's visualisation takes over again
	VA.make_current(second)
	check(await wait_rendering(second_visualisation), "second listener's visualisation never rendered after make_current() a second time")
	second.queue_free()
	await wait_frames(2)
	second = null
	second_visualisation = null
	check(await wait_rendering(scene_visualisation), "scene listener's visualisation never rendered after the current second listener was freed")

	check(rendered(source_visualisation) > 0, "source visualisation stopped while listeners were switched")

func add_visualisation(parent: Node) -> Node:
	var visualisation := VA.create_node(root.world, "VAVisualisation")
	VA.set_value(visualisation, "update_frequency_ms", UPDATE_FREQUENCY_MS)
	VA.set_value(visualisation, "max_distance", 0.0)
	parent.add_child(visualisation)
	return visualisation

# Visible instance count of the visualisation's multimesh, 0 when it isn't rendering
func rendered(visualisation: Node) -> int:
	for child in visualisation.get_children():
		if "multimesh" in child and child.multimesh != null:
			return child.multimesh.visible_instance_count
	return 0

# Polls until visualisation data arrives
func wait_rendering(visualisation: Node) -> bool:
	var started := Time.get_ticks_msec()
	while rendered(visualisation) == 0 and Time.get_ticks_msec() - started < RENDER_TIMEOUT_MS:
		await root.get_tree().process_frame
	return rendered(visualisation) > 0

func check_stays_stopped(visualisation: Node, reason: String) -> void:
	await root.get_tree().create_timer(QUIET_UPDATES * UPDATE_FREQUENCY_MS / 1000.0).timeout
	check(rendered(visualisation) == 0, reason)

func teardown() -> void:
	if is_instance_valid(second):
		second.free()
	for visualisation in [scene_visualisation, source_visualisation]:
		if is_instance_valid(visualisation):
			visualisation.free()
	VA.make_current(root.listener)
	await wait_raytraced()
