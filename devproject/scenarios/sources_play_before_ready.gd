# AI-created

extends "res://scenarios/lib/scenario.gd"

# play() called on a VASource or VASourceLeech before its emitter has been raytraced waits, and starts once it's ready to play, instead of being dropped. stop() cancels the waiting play

const SEED := 20261117

# Frames to give a cancelled source the chance to (wrongly) start
const WAIT_FRAMES := 30

var nodes: Array[Node] = []
var baseline := 0

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)
	baseline = await wait_grouped_eax_baseline()

func run() -> void:
	await check_source()
	await check_leech()
	await check_stop_cancels()

func check_source() -> void:
	var source := add_source("PlayBeforeReady")
	check(not VA.is_ready_to_play(source), "a source that was just added is already ready to play")
	check(VA.play(source), "play() before the source is ready returned false")
	check(await wait_playing(source, 15000), "a source play()ed before it was ready never started")
	check(VA.is_ready_to_play(source), "a source play()ed before it was ready started before it was ready")
	check(VA.is_raytraced(source), "a source started before it cast its own rays")

func check_leech() -> void:
	var emitter := VA.create_node(root.world, "VAEmitter")
	emitter.name = "PlayBeforeReadyEmitter"
	emitter.position = root.source.position
	root.add_child(emitter)
	nodes.append(emitter)

	var leech := spawn_leech("PlayBeforeReadyLeech", emitter)

	check(not VA.is_ready_to_play(leech), "a leech that was just added is already ready to play")
	check(VA.play(leech), "play() before the leech is ready returned false")
	check(await wait_playing(leech, 15000), "a leech play()ed before it was ready never started")
	check(VA.is_ready_to_play(leech), "a leech play()ed before it was ready started before it was ready")
	check(VA.is_raytraced(leech), "a leech started before its emitter cast its own rays")

func check_stop_cancels() -> void:
	var source := add_source("StopBeforeReady")
	VA.play(source)
	VA.stop(source)
	await wait_until(func(): return VA.is_ready_to_play(source), 15000)
	await wait_frames(WAIT_FRAMES)
	check(not VA.is_playing(source), "a source play()ed then stopped before it was ready played once ready")

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
