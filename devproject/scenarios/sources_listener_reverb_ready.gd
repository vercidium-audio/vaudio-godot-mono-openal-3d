# AI-created

extends "res://scenarios/lib/scenario.gd"

# A raytraced source that doesn't affect grouped EAX but uses the listener's reverb waits for the listener's first reverb results before it plays, so it never starts in the default reverb. A VASourceRelative is the probe for when the listener's reverb is ready. When the listener casts no reverb rays, sources that use its reverb (raytraced, ambient and relative) play without reverb and warn exactly once each

const READY_TIMEOUT_MS := 15000

# Given to the listener, so it has reverb results to wait for and an ambient filter
const LISTENER_RAYS := 64

# Frames a source gets to warn again after it started playing
const WAIT_FRAMES := 30

func expect_no_listener_reverb_warning(node_name: String) -> void:
	expect_warning("%s: The listener '%s' casts no reverb rays" % [node_name, root.listener.name], 1)

func _init() -> void:
	scene = REVERB_ROOM
	backend_smoke = true
	timeout_seconds = 120.0

func run() -> void:
	await check_waits_for_listener_reverb()
	await check_listener_without_reverb()

# The listener's first reverb results normally arrive in the same pass as its first raytrace of the source, so the source is raytraced first with listener reverb off, then the listener's reverb is turned on and has no results yet. The source's emitter warns while the listener has no reverb rays, and not again after
func check_waits_for_listener_reverb() -> void:
	await reload_scene(0)
	var probe := add_relative("WaitingProbe")
	var source := add_source("WaitingSource", false)
	expect_no_listener_reverb_warning(source.name + "-Emitter")

	await wait_raytraced_by_listener(source)
	check(VA.is_ready_to_play(source), "a source with use_listener_reverb wasn't ready to play with a listener that casts no reverb rays")

	for property in ["reverb_ray_count", "reverb_bounce_count"]:
		VA.set_value(root.listener, property, LISTENER_RAYS)
	check(not VA.is_ready_to_play(probe), "the listener's reverb was ready straight after its reverb rays were turned on")
	# Its return value isn't checked, as it's also false while the stream decodes
	VA.play(source)

	var started := Time.get_ticks_msec()
	while not VA.is_ready_to_play(probe) and Time.get_ticks_msec() - started < READY_TIMEOUT_MS:
		check(not VA.is_ready_to_play(source), "a source with use_listener_reverb was ready to play before the listener had reverb results")
		check(not VA.is_playing(source), "a source with use_listener_reverb started playing before the listener had reverb results")
		await root.get_tree().process_frame

	check(VA.is_ready_to_play(probe), "the listener never got reverb results")
	check(await wait_playing(source, READY_TIMEOUT_MS), "a source with use_listener_reverb never started once the listener had reverb results")
	check(VA.has_reverb(source), "a source with use_listener_reverb has no reverb effect")

# The scene's listener casts no reverb rays, so there's nothing to wait for
func check_listener_without_reverb() -> void:
	await reload_scene(0)
	var sources := [add_source("UnreverbedSource"), add_ambient("UnreverbedAmbient"), add_relative("UnreverbedRelative", true)]
	# A VASource warns through its emitter
	expect_no_listener_reverb_warning(sources[0].name + "-Emitter")
	expect_no_listener_reverb_warning(sources[1].name)
	expect_no_listener_reverb_warning(sources[2].name)

	for source in sources:
		check(await wait_playing(source, READY_TIMEOUT_MS), "%s never started with a listener that casts no reverb rays" % source.name)

	await wait_frames(WAIT_FRAMES)

# Looping, so a source that started early is still playing when checked
func add_source(source_name: String, autoplay := true) -> Node:
	var source := VA.create_node(root.world, "VASource")
	source.name = source_name
	source.position = root.source.position
	VA.set_value(source, "streams", VA.get_value(root.source, "streams"))
	VA.set_value(source, "looping", true)
	VA.set_value(source, "autoplay", autoplay)
	VA.set_value(source, "affects_grouped_eax", false)
	VA.set_value(source, "use_listener_reverb", true)
	root.add_child(source)
	return source

func add_ambient(source_name: String) -> Node:
	var source := VA.create_node(root.world, "VASourceAmbient")
	source.name = source_name
	VA.set_value(source, "streams", VA.get_value(root.source, "streams"))
	VA.set_value(source, "looping", true)
	VA.set_value(source, "use_listener_reverb", true)
	root.add_child(source)
	return source

func add_relative(source_name: String, autoplay := false) -> Node:
	var source := VA.create_node(root.world, "VASourceRelative")
	source.name = source_name
	VA.set_value(source, "streams", VA.get_value(root.source, "streams"))
	VA.set_value(source, "looping", true)
	VA.set_value(source, "autoplay", autoplay)
	root.add_child(source)
	return source

# A fresh VAWorld, whose listener has no reverb results yet. The scene's listener casts no reverb rays, so it's given listener_reverb_rays, and always LISTENER_RAYS ambient rays for the ambient filter (-1 leaves both). The added sources are freed with the old scene
func reload_scene(listener_reverb_rays := -1) -> void:
	var tree := root.get_tree()
	tree.reload_current_scene()
	await tree.scene_changed
	root = tree.current_scene
	if listener_reverb_rays >= 0:
		for property in ["reverb_ray_count", "reverb_bounce_count"]:
			VA.set_value(root.listener, property, listener_reverb_rays)
		for property in ["ambient_occlusion_ray_count", "ambient_occlusion_bounce_count"]:
			VA.set_value(root.listener, property, LISTENER_RAYS)

# Leaves an unmodified, raytraced scene for the next scenario
func teardown() -> void:
	await reload_scene()
	await wait_raytraced_by_listener(root.source)
