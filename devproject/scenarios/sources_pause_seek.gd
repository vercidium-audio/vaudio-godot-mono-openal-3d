# AI-created

extends "res://scenarios/lib/scenario.gd"

# stream_paused pauses and resumes a playing source without losing its place, seek() moves it while playing or paused, and play(start_time) starts part way through, including a play that waits for the source to be ready. A source played while paused starts paused.

const SEED := 20261119

# speech.ogg is long enough that none of these times reach the end
const SEEK_TIME := 10.0
const PAUSED_SEEK_TIME := 5.0
const PLAY_START_TIME := 8.0
const PENDING_PLAY_START_TIME := 12.0

# OpenAL Soft reports the offset at its mixing granularity, and play() starts a frame or two after the call
const TIME_TOLERANCE := 0.25

var nodes: Array[Node] = []
var baseline := 0

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)
	baseline = await wait_grouped_eax_baseline()

func run() -> void:
	var source := add_source("PauseSeek")
	await wait_raytraced_by_listener(source)

	check(VA.play(source), "play() returned false")
	check(await wait_playing(source, 15000), "the source never started playing")
	await step("speech playing", 0.5)

	await check_pause_resume(source)
	await check_seek(source)
	await check_seek_while_paused(source)
	await check_play_from(source)
	await check_play_while_paused(source)
	await check_pending_play_from()

func check_pause_resume(source: Node) -> void:
	VA.set_stream_paused(source, true)
	check(VA.is_stream_paused(source), "stream_paused didn't stick")
	check(not VA.is_playing(source), "is_playing() is true while paused")

	var paused_at := VA.playback_time(source)
	check(paused_at > 0.0, "the playback time is 0 after playing for a while")
	await step("silence, paused", 0.5)
	check(is_equal_approx(VA.playback_time(source), paused_at), "the playback time moved while paused (%.3f -> %.3f)" % [paused_at, VA.playback_time(source)])

	VA.set_stream_paused(source, false)
	check(VA.is_playing(source), "is_playing() is false after resuming")
	await step("speech resumed where it paused", 0.5)
	check(VA.playback_time(source) > paused_at, "the playback time didn't move after resuming")

func check_seek(source: Node) -> void:
	VA.seek(source, SEEK_TIME)
	check_time(source, SEEK_TIME, "seek() while playing")
	await step("speech skipped ahead", 0.5)
	check(VA.playback_time(source) > SEEK_TIME, "the playback time didn't move after seek()")
	check(VA.is_playing(source), "the source stopped after seek()")

func check_seek_while_paused(source: Node) -> void:
	VA.set_stream_paused(source, true)
	VA.seek(source, PAUSED_SEEK_TIME)
	check_time(source, PAUSED_SEEK_TIME, "seek() while paused")
	check(not VA.is_playing(source), "seek() while paused resumed the source")
	await step("silence, paused", 0.25)
	check_time(source, PAUSED_SEEK_TIME, "seek() while paused, after waiting")

	VA.set_stream_paused(source, false)
	await step("speech resumed from the seek time", 0.5)
	check(VA.playback_time(source) > PAUSED_SEEK_TIME, "the source didn't resume from the time it seeked to while paused")

func check_play_from(source: Node) -> void:
	VA.stop(source)
	check(not VA.is_playing(source), "stop() didn't stop the source")
	check(VA.play_from(source, PLAY_START_TIME), "play(start_time) returned false")
	check(await wait_playing(source), "play(start_time) never started")
	check_time(source, PLAY_START_TIME, "play(start_time)")
	await step("speech playing part way through", 0.5)

func check_play_while_paused(source: Node) -> void:
	VA.stop(source)
	VA.set_stream_paused(source, true)
	check(VA.play(source), "play() while paused returned false")
	await wait_frames(10)
	check(not VA.is_playing(source), "play() while paused started the sound")
	check_time(source, 0.0, "play() while paused")

	VA.set_stream_paused(source, false)
	check(await wait_playing(source), "a sound played while paused didn't start after resuming")
	await step("speech playing from the start", 0.5)
	VA.stop(source)

# play(start_time) on a source that isn't ready yet waits, then starts from that time
func check_pending_play_from() -> void:
	var source := add_source("PendingPlayFrom")
	check(not VA.is_ready_to_play(source), "a source that was just added is already ready to play")
	check(VA.play_from(source, PENDING_PLAY_START_TIME), "play(start_time) before the source is ready returned false")
	check(await wait_playing(source, 15000), "a source play()ed from a start time before it was ready never started")
	check_time(source, PENDING_PLAY_START_TIME, "play(start_time) before ready")
	VA.stop(source)

func check_time(source: Node, expected: float, label: String) -> void:
	var time := VA.playback_time(source)
	check(time >= expected - 0.01 and time < expected + TIME_TOLERANCE, "%s: the playback time is %.3f, expected %.3f" % [label, time, expected])

# Non-looping and not autoplaying, so only play() can start it
func add_source(source_name: String) -> Node:
	var source := spawn_source(source_name, root.source.position, SEED + nodes.size(), false, false)
	nodes.append(source)
	return source

func teardown() -> void:
	for node in nodes:
		if is_instance_valid(node):
			node.queue_free()

	# The freed sources' reverb tails linger, which would skew the next scenario's baseline
	await wait_grouped_eax_count(baseline)
