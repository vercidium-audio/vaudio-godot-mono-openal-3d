# AI-created

extends "res://scenarios/lib/scenario.gd"

# A VASourceRelative with use_listener_reverb off is a plain listener-relative player (music, UI): it plays straight away without waiting for the listener's reverb, and sends to no reverb bus. Replaces 1.11's ALSourceRelative

const REVERB_RAYS := 64

func _init() -> void:
	scene = REVERB_ROOM
	backend_smoke = true

func run() -> void:
	var defaults := VA.create_node(root.world, "VASourceRelative")
	check(VA.get_value(defaults, "use_listener_reverb") == true, "VASourceRelative's use_listener_reverb doesn't default to true")
	defaults.free()

	# A fresh world, so the listener has reverb results still to come
	await reload_scene(REVERB_RAYS)
	var source := VA.create_node(root.world, "VASourceRelative")
	source.name = "RelativeNoReverb"
	VA.set_value(source, "streams", VA.get_value(root.source, "streams"))
	VA.set_value(source, "looping", true)
	VA.set_value(source, "use_listener_reverb", false)
	VA.set_value(source, "autoplay", true)
	root.add_child(source)

	check(VA.is_ready_to_play(source), "a relative source without listener reverb wasn't ready to play straight away")
	check(await wait_playing(source), "an autoplaying relative source without listener reverb never started")
	check(not VA.has_reverb(source), "a relative source without listener reverb was sent to a reverb bus")
	check(VA.is_dry_output_enabled(source), "a relative source without listener reverb had its dry output disabled")

func reload_scene(listener_reverb_rays := -1) -> void:
	var tree := root.get_tree()
	tree.reload_current_scene()
	await tree.scene_changed
	root = tree.current_scene
	if listener_reverb_rays >= 0:
		VA.set_value(root.listener, "reverb_ray_count", listener_reverb_rays)
		VA.set_value(root.listener, "reverb_bounce_count", listener_reverb_rays)

# Leaves an unmodified, raytraced scene for the next scenario
func teardown() -> void:
	await reload_scene()
	await wait_raytraced_by_listener(root.source)
