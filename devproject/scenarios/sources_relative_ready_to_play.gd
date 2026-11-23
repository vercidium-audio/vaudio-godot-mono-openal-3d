extends "res://scenarios/lib/scenario.gd"

# A VASourceRelative waits for the current listener's first reverb results before it plays, so it never starts in the default OpenAL reverb. autoplay and play() wait, and stop() cancels a waiting play. A listener that never gets reverb results (no reverb rays, or outside the bounds) is ready straight away. Each case reloads the scene, since the listener's results stay with the world across listener switches

const READY_TIMEOUT_MS := 15000

# Given to the listener, so it has reverb results to wait for
const REVERB_RAYS := 64

# Frames to give a cancelled source the chance to (wrongly) start
const WAIT_FRAMES := 30

func _init() -> void:
	scene = REVERB_ROOM
	timeout_seconds = 120.0

func run() -> void:
	await check_autoplay()
	await check_play_before_ready()
	await check_stop_cancels()
	await check_no_reverb_rays()
	await check_outside_bounds()

func check_autoplay() -> void:
	await reload_scene(REVERB_RAYS)
	var source := add_relative("RelativeAutoplay", true)
	check(not VA.is_ready_to_play(source), "a relative source was ready to play before the listener had reverb results")
	check(await wait_ready_without_playing(source), "a relative source never became ready to play")
	check(await wait_playing(source), "an autoplaying relative source never started once ready")

func check_play_before_ready() -> void:
	await reload_scene(REVERB_RAYS)
	var source := add_relative("RelativePlayBeforeReady", false)
	check(not VA.is_ready_to_play(source), "a relative source was ready to play before the listener had reverb results")
	check(VA.play(source), "play() on a relative source before it was ready returned false")
	check(await wait_ready_without_playing(source), "a relative source play()ed before it was ready never became ready")
	check(await wait_playing(source), "a relative source play()ed before it was ready never started")

func check_stop_cancels() -> void:
	await reload_scene(REVERB_RAYS)
	var autoplay := add_relative("RelativeStopAutoplay", true)
	var played := add_relative("RelativeStopPlayed", false)
	VA.play(played)
	VA.stop(autoplay)
	VA.stop(played)
	check(await wait_until(func(): return VA.is_ready_to_play(played), READY_TIMEOUT_MS), "a relative source never became ready to play")
	await wait_frames(WAIT_FRAMES)
	check(not VA.is_playing(autoplay), "an autoplaying relative source stopped before it was ready played once ready")
	check(not VA.is_playing(played), "a relative source play()ed then stopped before it was ready played once ready")

# The listener never gets reverb results, so there's nothing to wait for
func check_no_reverb_rays() -> void:
	await reload_scene(0)
	await check_ready_straight_away("RelativeNoReverbRays", "with no reverb rays")

func check_outside_bounds() -> void:
	await reload_scene(REVERB_RAYS)
	# A clamped listener is raytraced at the bounds' edge, so it would still get reverb results
	VA.set_value(root.listener, "clamp_position", false)
	root.listener.position = VA.bounds_position(root.world) - VA.get_value(root.world, "bounds_size")
	await check_ready_straight_away("RelativeListenerOutside", "outside the bounds")

func check_ready_straight_away(source_name: String, listener_state: String) -> void:
	var source := add_relative(source_name, false)
	var count := VA.raytrace_count(root.world)
	check(await wait_until(func(): return VA.is_ready_to_play(source)), "a relative source never became ready to play with the listener %s" % listener_state)
	print("[devproject] listener %s: ready after %d raytrace passes" % [listener_state, VA.raytrace_count(root.world) - count])
	check(VA.play(source), "play() on a relative source with the listener %s returned false" % listener_state)
	check(await wait_playing(source), "a relative source with the listener %s never started" % listener_state)

# Fails if the source starts playing before it's ready. Returns whether it became ready before the timeout
func wait_ready_without_playing(source: Node) -> bool:
	var started := Time.get_ticks_msec()
	while not VA.is_ready_to_play(source) and Time.get_ticks_msec() - started < READY_TIMEOUT_MS:
		check(not VA.is_playing(source), "a relative source started playing before the listener had reverb results")
		await root.get_tree().process_frame
	return VA.is_ready_to_play(source)

# Looping, so a source that started early is still playing when checked
func add_relative(source_name: String, autoplay: bool) -> Node:
	var source := VA.create_node(root.world, "VASourceRelative")
	source.name = source_name
	VA.set_value(source, "streams", VA.get_value(root.source, "streams"))
	VA.set_value(source, "looping", true)
	VA.set_value(source, "autoplay", autoplay)
	root.add_child(source)
	return source

# A fresh VAWorld, whose listener has no reverb results yet. The scene's listener casts no reverb rays, so it's given listener_reverb_rays (-1 leaves it). The added sources are freed with the old scene
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
