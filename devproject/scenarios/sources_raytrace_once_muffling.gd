extends "res://scenarios/lib/scenario.gd"

# raytrace_once also stops the listener raytracing the source, so its muffling is held at its first result while the listener moves behind a brick partition. Checked while its reverb tail keeps its emitter in the world, since that's when the listener used to keep updating it

var partition: Node
var start_position
var once: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	start_position = root.listener.global_position
	partition = DIM.add_reverb_room_partition(root, "brick")
	root.listener.global_position = DIM.REVERB_ROOM_LOS_LISTENER
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	once = spawn_source("RaytraceOnce", root.source.position, 71, true, true, true)

	# Raytraced once, then out of the listener's targets but still ready to play while its tail plays
	var held_state := func(): return VA.is_ready_to_play(once) and not VA.is_raytraced_by_listener(once)
	check(await wait_until(held_state), "raytrace_once source never left the listener's targets while ready to play")

	var held := Vector2(VA.muffling_lf(once), VA.muffling_hf(once))
	check(held.y > 0.9, "held muffling HF %.4f with line of sight, expected almost unmuffled" % held.y)

	root.listener.global_position = start_position
	await wait_raytraced(3)

	if VA.is_ready_to_play(once):
		check(VA.muffling_lf(once) == held.x and VA.muffling_hf(once) == held.y, "raytrace_once muffling changed from LF %.4f HF %.4f to LF %.4f HF %.4f after the listener moved behind the wall" % [held.x, held.y, VA.muffling_lf(once), VA.muffling_hf(once)])
	else:
		print("[devproject] raytrace_once source's reverb tail finished before the listener moved, so only the source was checked")

	# The listener really did move behind the wall
	var behind := await measure_muffling("source behind wall", "listener behind the brick wall - expect the raytrace-once source to stay clear")
	check(behind.y < held.y * 0.5, "scene source muffling HF %.4f behind the wall isn't below half the held gain %.4f" % [behind.y, held.y])
	check(VA.is_playing(once), "raytrace_once source stopped playing after its muffling was held")

func teardown() -> void:
	if is_instance_valid(once):
		once.queue_free()
	root.listener.global_position = start_position
	partition.queue_free()
	await wait_raytraced()
