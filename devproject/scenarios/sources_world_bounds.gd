extends "res://scenarios/lib/scenario.gd"

# World bounds rules for raytraced sources, with the bounds shrunk to end at x = 0 like world_bounds.gd so the scene source's side is outside. With emitters_outside_the_world_are_muffled on, a non-looping source outside doesn't play, a playing source that leaves stops, and a looping source or stream waits outside and resumes inside. With it off, sources outside play clear with no reverb

const SEED := 20261109
const AL_FORMAT_MONO16 := 4353
const FREQUENCY := 44100

# Frames to give a source the chance to (wrongly) start
const WAIT_FRAMES := 30

const OUTSIDE_WARNING := "outside the VAWorld's bounds"

var original_size
var original_muffled: bool
var original_clamp: bool
var shrunk
var inside
var outside
var nodes: Array[Node] = []
var baseline := 0

func _init() -> void:
	scene = REVERB_ROOM
	timeout_seconds = 180.0

func setup() -> void:
	original_size = VA.get_value(root.world, "bounds_size")
	original_muffled = VA.get_value(root.world, "emitters_outside_the_world_are_muffled")
	original_clamp = VA.get_value(root.source, "clamp_position")

	# The bounds start at the world's position, so this ends them at x = 0 - the listener is at negative x, the source at positive x
	shrunk = original_size
	shrunk.x = -root.world.position.x
	outside = root.source.position
	inside = root.source.position
	inside.x = -inside.x

	await wait_raytraced_by_listener(root.source)
	await wait_raytraced(SETTLE_PASSES)
	baseline = VA.grouped_eax_count(root.world)

func run() -> void:
	VA.set_value(root.world, "emitters_outside_the_world_are_muffled", true)
	await check_helpers()
	await check_non_looping()
	await check_looping()
	await check_stop_cancels()
	await check_stream()
	await check_clamped()
	await check_setting_off()

func check_helpers() -> void:
	shrink(true)
	var corner = VA.bounds_position(root.world)
	check(VA.is_within_bounds(root.world, inside), "a point on the listener's side isn't within the shrunk bounds")
	check(not VA.is_within_bounds(root.world, outside), "the source's position is within the shrunk bounds")
	check(VA.is_within_bounds(root.world, corner) and VA.is_within_bounds(root.world, corner + shrunk), "the bounds' edges aren't within them")
	check(not VA.is_within_bounds(root.world, corner + shrunk + DIM.planar(0.01, 0)), "a point just past the bounds' edge is within them")
	check(VA.is_silenced_by_bounds(root.source), "unclamped source outside isn't silenced")

	VA.set_value(root.source, "clamp_position", true)
	check(not VA.is_silenced_by_bounds(root.source), "clamped source outside is silenced")
	VA.set_value(root.source, "clamp_position", original_clamp)

	VA.set_value(root.world, "emitters_outside_the_world_are_muffled", false)
	check(not VA.is_silenced_by_bounds(root.source), "source outside is silenced with emitters_outside_the_world_are_muffled off")
	VA.set_value(root.world, "emitters_outside_the_world_are_muffled", true)
	shrink(false)

func check_non_looping() -> void:
	shrink(true)

	var autoplayed := add_source("NonLoopAutoplay", outside, false, true)
	await wait_raytraced_by_listener(autoplayed)
	await wait_frames(WAIT_FRAMES)
	check(not VA.is_playing(autoplayed), "non-looping autoplay source started outside the bounds")

	var played := add_source("NonLoopPlayed", outside, false, false)
	await wait_raytraced_by_listener(played)
	expect_warning(OUTSIDE_WARNING)
	check(not VA.play(played), "play() of a non-looping source outside the bounds returned true")
	await wait_frames(WAIT_FRAMES)
	check(not VA.is_playing(played), "non-looping source played outside the bounds")

	# Neither retries once inside
	autoplayed.position = inside
	played.position = inside
	await wait_until(func(): return VA.is_ready_to_play(autoplayed) and VA.is_ready_to_play(played))
	await wait_frames(WAIT_FRAMES)
	check(not VA.is_playing(autoplayed), "non-looping autoplay source started once moved inside the bounds")
	check(not VA.is_playing(played), "non-looping source played once moved inside the bounds")

	# A playing one that leaves is cut off, and doesn't resume when it's back
	check(await wait_until(func(): return VA.play(played)), "non-looping source didn't play inside the bounds")
	check(await wait_playing(played), "non-looping source isn't playing inside the bounds")
	await step("non-looping source playing inside the bounds", 0.5)
	played.position = outside
	await wait_frames(2)
	check(not VA.is_playing(played), "non-looping source still playing after leaving the bounds")
	played.position = inside
	await wait_frames(WAIT_FRAMES)
	check(not VA.is_playing(played), "non-looping source resumed after coming back inside the bounds")

	shrink(false)

func check_looping() -> void:
	shrink(true)

	# Waits outside, then plays once the bounds move to include it, stops when they move away, and plays again
	var autoplayed := add_source("LoopAutoplay", outside, true, true)
	await wait_raytraced_by_listener(autoplayed)
	await wait_frames(WAIT_FRAMES)
	check(not VA.is_playing(autoplayed), "looping autoplay source started outside the bounds")
	shrink(false)
	check(await wait_playing(autoplayed), "looping autoplay source didn't start once the bounds included it")
	await step("looping source playing once the bounds include it", 0.5)
	shrink(true)
	await wait_frames(2)
	check(not VA.is_playing(autoplayed), "looping source still playing after the bounds left it")
	shrink(false)
	check(await wait_playing(autoplayed), "looping source didn't resume once the bounds included it again")

	# play() outside waits without a warning, then the source moving in and out stops and resumes it
	shrink(true)
	var played := add_source("LoopPlayed", outside, true, false)
	await wait_raytraced_by_listener(played)
	check(not VA.play(played), "play() of a looping source outside the bounds returned true")
	await wait_frames(WAIT_FRAMES)
	check(not VA.is_playing(played), "looping source played outside the bounds")
	played.position = inside
	check(await wait_playing(played), "looping source play()ed outside didn't start once moved inside")
	played.position = outside
	await wait_frames(2)
	check(not VA.is_playing(played), "looping source still playing after moving outside the bounds")
	played.position = inside
	check(await wait_playing(played), "looping source didn't resume once moved back inside")

	shrink(false)

func check_stop_cancels() -> void:
	shrink(true)

	var played := add_source("StopPlayed", outside, true, false)
	var autoplayed := add_source("StopAutoplay", outside, true, true)
	await wait_raytraced_by_listener(played)
	await wait_raytraced_by_listener(autoplayed)
	VA.play(played)
	VA.stop(played)
	VA.stop(autoplayed)

	played.position = inside
	autoplayed.position = inside
	await wait_until(func(): return VA.is_ready_to_play(played) and VA.is_ready_to_play(autoplayed))
	await wait_frames(WAIT_FRAMES)
	check(not VA.is_playing(played), "looping source play()ed outside then stopped played once inside")
	check(not VA.is_playing(autoplayed), "looping autoplay source stopped outside played once inside")

	shrink(false)

func check_stream() -> void:
	shrink(true)

	var stream := VA.create_node(root.world, "VAStreamSource")
	stream.name = "BoundsStream"
	stream.position = outside
	VA.set_value(stream, "reverb_ray_count", 64)
	VA.set_value(stream, "reverb_bounce_count", 64)
	VA.set_value(stream, "scattering_seed", SEED)
	root.add_child(stream)
	nodes.append(stream)

	check(VA.open_stream(stream, AL_FORMAT_MONO16, FREQUENCY), "open_stream outside the bounds failed")
	check(VA.is_stream_open(stream), "stream opened outside the bounds isn't open")
	await wait_frames(WAIT_FRAMES)
	check(not VA.is_playing(stream), "stream opened outside the bounds is playing")

	stream.position = inside
	check(await wait_playing(stream), "stream opened outside didn't start once moved inside")
	VA.push_audio_data(stream, sine_wave(0.5))
	stream.position = outside
	await wait_frames(2)
	check(not VA.is_playing(stream), "stream still playing after leaving the bounds")
	check(VA.is_stream_open(stream), "stream was closed by leaving the bounds")
	stream.position = inside
	check(await wait_playing(stream), "stream didn't restart once back inside")

	# Only a stream the bounds stopped restarts
	VA.close_stream(stream)
	stream.position = outside
	await wait_frames(2)
	stream.position = inside
	await wait_frames(WAIT_FRAMES)
	check(not VA.is_playing(stream), "closed stream restarted after leaving and re-entering the bounds")

	shrink(false)

func check_clamped() -> void:
	VA.set_value(root.source, "clamp_position", true)
	shrink(true)
	await wait_frames(WAIT_FRAMES)
	check(VA.is_playing(root.source), "clamped source outside the bounds stopped playing")

	var clamped := add_source("Clamped", outside, false, false)
	VA.set_value(clamped, "clamp_position", true)
	await wait_raytraced_by_listener(clamped)
	check(await wait_until(func(): return VA.play(clamped)), "non-looping clamped source outside the bounds didn't play")

	VA.set_value(root.source, "clamp_position", original_clamp)
	shrink(false)

func check_setting_off() -> void:
	VA.set_value(root.world, "emitters_outside_the_world_are_muffled", false)
	shrink(true)

	await wait_frames(WAIT_FRAMES)
	check(VA.is_playing(root.source), "unclamped source outside stopped with emitters_outside_the_world_are_muffled off")
	check(not VA.has_reverb(root.source), "source outside the bounds still has a reverb effect")

	# New sources outside play straight away, without waiting for their own grouped EAX, and get reverb once raytraced inside
	var played := add_source("OffPlayed", outside, false, false)
	var once := add_source("OffRaytraceOnce", outside, true, true)
	VA.set_value(once, "raytrace_once", true)
	await wait_raytraced_by_listener(played)
	check(await wait_until(func(): return VA.play(played)), "non-looping source outside didn't play with emitters_outside_the_world_are_muffled off")
	check(await wait_playing(once), "raytrace_once source outside didn't autoplay with emitters_outside_the_world_are_muffled off")
	check(not VA.has_reverb(played) and not VA.has_reverb(once), "new sources outside the bounds have a reverb effect")
	await step("sources outside the bounds playing clear with no reverb", 1.0)

	shrink(false)
	check(await wait_until(func(): return VA.has_reverb(root.source) and VA.has_reverb(played) and VA.has_reverb(once)), "sources didn't get a reverb effect once inside the bounds")
	check(VA.is_playing(once), "raytrace_once source stopped once inside the bounds")
	VA.set_value(root.world, "emitters_outside_the_world_are_muffled", true)

func add_source(source_name: String, position, looping: bool, autoplay: bool) -> Node:
	var source := spawn_source(source_name, position, SEED + nodes.size(), looping, autoplay)
	nodes.append(source)
	return source

func shrink(value: bool) -> void:
	VA.set_value(root.world, "bounds_size", shrunk if value else original_size)

func restore() -> void:
	VA.set_value(root.source, "clamp_position", original_clamp)
	VA.set_value(root.world, "emitters_outside_the_world_are_muffled", original_muffled)
	VA.set_value(root.world, "bounds_size", original_size)

func teardown() -> void:
	restore()
	for node in nodes:
		if is_instance_valid(node):
			node.queue_free()

	# The freed sources' reverb tails linger, which would skew the next scenario's baseline
	await wait_grouped_eax_count(baseline)

# Signed 16-bit mono 440 Hz sine wave, little endian
func sine_wave(seconds: float) -> PackedByteArray:
	var frames := int(FREQUENCY * seconds)
	var data := PackedByteArray()
	data.resize(frames * 2)
	for i in frames:
		data.encode_s16(i * 2, int(sin(TAU * 440.0 * i / FREQUENCY) * 8000.0))
	return data
