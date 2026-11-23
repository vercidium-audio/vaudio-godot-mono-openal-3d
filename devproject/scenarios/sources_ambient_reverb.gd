extends "res://scenarios/lib/scenario.gd"

# A VASourceAmbient has no reverb by default. With use_listener_reverb it's sent to the listener's reverb effect, which can be toggled while playing, and it waits for the listener's first reverb results before it plays, like VASourceRelative. A VASourceRelative is the probe for when the listener's reverb is ready

const READY_TIMEOUT_MS := 15000

# Given to the listener, so it has reverb results to wait for and an ambient filter
const LISTENER_RAYS := 64

func _init() -> void:
	scene = REVERB_ROOM
	timeout_seconds = 120.0

func run() -> void:
	await reload_scene()

	var probe := VA.create_node(root.world, "VASourceRelative")
	probe.name = "AmbientReverbProbe"
	root.add_child(probe)

	var plain := add_ambient("AmbientNoReverb", false)
	var reverb := add_ambient("AmbientListenerReverb", true)

	# Fails if the listener reverb source starts before the listener's reverb is ready
	var started := Time.get_ticks_msec()
	while not VA.is_playing(reverb) and Time.get_ticks_msec() - started < READY_TIMEOUT_MS:
		await root.get_tree().process_frame
		check(not VA.is_playing(reverb) or VA.is_ready_to_play(probe), "an ambient source with use_listener_reverb started before the listener had reverb results")

	check(VA.is_playing(reverb), "an ambient source with use_listener_reverb never started")
	check(await wait_playing(plain), "an ambient source without use_listener_reverb never started")
	check(not VA.has_reverb(plain), "an ambient source without use_listener_reverb has a reverb effect")
	check(VA.has_reverb(reverb), "an ambient source with use_listener_reverb has no reverb effect")

	# Toggled while playing
	VA.set_value(plain, "use_listener_reverb", true)
	VA.set_value(reverb, "use_listener_reverb", false)
	await wait_frames(2)
	check(VA.has_reverb(plain), "turning on use_listener_reverb while playing didn't add the reverb effect")
	check(not VA.has_reverb(reverb), "turning off use_listener_reverb while playing didn't remove the reverb effect")

	await step("ambient source with listener reverb", 1.0)

func add_ambient(node_name: String, use_listener_reverb: bool) -> Node:
	var node := VA.create_node(root.world, "VASourceAmbient")
	node.name = node_name
	VA.set_value(node, "streams", VA.get_value(root.source, "streams"))
	VA.set_value(node, "looping", true)
	VA.set_value(node, "use_listener_reverb", use_listener_reverb)
	root.add_child(node)
	return node

# A fresh VAWorld, whose listener has no reverb results yet. The added sources are freed with the old scene
func reload_scene(listener_rays := LISTENER_RAYS) -> void:
	var tree := root.get_tree()
	tree.reload_current_scene()
	await tree.scene_changed
	root = tree.current_scene
	if listener_rays >= 0:
		for property in ["reverb_ray_count", "reverb_bounce_count", "ambient_occlusion_ray_count", "ambient_occlusion_bounce_count"]:
			VA.set_value(root.listener, property, listener_rays)

# Leaves an unmodified, raytraced scene for the next scenario
func teardown() -> void:
	await reload_scene(-1)
	await wait_raytraced_by_listener(root.source)
