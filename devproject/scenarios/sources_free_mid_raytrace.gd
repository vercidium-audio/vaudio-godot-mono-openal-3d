extends "res://scenarios/lib/scenario.gd"

# Raises a source's reverb ray count and frees it 0-3 frames later, so the free races the work item the change queued. Each round varies the gap, queue_free() vs immediate free(), and whether the source has finished its first raytrace yet. Any error, crash or stalled world fails it

const ROUNDS := 16
const RAISED_RAY_COUNT := 4096
const SEED := 20260930
const STALL_TIMEOUT_MS := 5000

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	await wait_raytraced(SETTLE_PASSES)
	var baseline := VA.grouped_eax_count(root.world)
	var rng := RandomNumberGenerator.new()
	rng.seed = SEED

	for round in ROUNDS:
		var gap_frames := round % 4
		var immediate := (round / 4) % 2 == 1
		var before_first_raytrace := round >= ROUNDS / 2

		var source := spawn_source("Racer%d" % round, DIM.random_reverb_room_point(rng), rng.randi())
		if not before_first_raytrace:
			await wait_raytraced_by_listener(source)

		VA.set_value(source, "reverb_ray_count", RAISED_RAY_COUNT)
		await wait_frames(gap_frames)
		if immediate:
			source.free()
		else:
			source.queue_free()

		# Let the in-flight work item land before the next round
		await wait_raytraced()
		if listen:
			print(">> round %d: freed %d frame(s) after raising ray count, %s, %s" % [round, gap_frames, "free()" if immediate else "queue_free()", "before first raytrace" if before_first_raytrace else "after first raytrace"])

	# The world must still be raytracing, not wedged by a work item for a freed emitter
	var before := VA.raytrace_count(root.world)
	var started := Time.get_ticks_msec()
	while VA.raytrace_count(root.world) < before + SETTLE_PASSES and Time.get_ticks_msec() - started < STALL_TIMEOUT_MS:
		await root.get_tree().process_frame
	var passes := VA.raytrace_count(root.world) - before
	check(passes >= SETTLE_PASSES, "only %d raytrace pass(es) completed in %d s after the rounds, expected the world to keep raytracing" % [passes, STALL_TIMEOUT_MS / 1000])

	var after := await wait_grouped_eax_count(baseline)
	var muffling := await measure_muffling("after %d rounds" % ROUNDS, "all racers freed - expect only the original voice")
	print("[devproject] %d rounds -> grouped EAX count %d (baseline %d)" % [ROUNDS, after, baseline])

	check(after == baseline, "grouped EAX count %d after freeing every racer didn't return to the baseline %d" % [after, baseline])
	check(VA.is_raytraced_by_listener(root.source), "original source is no longer raytraced after the rounds")
	check(muffling.y > 0.9, "original source's muffling HF %.4f after the rounds, expected it still unmuffled in the open room" % muffling.y)

func teardown() -> void:
	for child in root.get_children():
		if child.name.begins_with("Racer"):
			child.queue_free()
	await wait_raytraced()
