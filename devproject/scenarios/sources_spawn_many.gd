extends "res://scenarios/lib/scenario.gd"

# Spawns 32 playing VASources at fixed-seed random spots in the reverb room, then frees them all in one frame. Grouped reverb must stay within maximum_grouped_eax_count throughout and return to its baseline once they're gone

const SPAWN_COUNT := 32
const SEED := 20260929

var spawned: Array[Node] = []

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	await wait_raytraced(SETTLE_PASSES)
	var maximum: int = VA.get_value(root.world, "maximum_grouped_eax_count")
	var baseline := VA.grouped_eax_count(root.world)
	print("[devproject] baseline -> grouped EAX count %d (maximum %d)" % [baseline, maximum])

	var rng := RandomNumberGenerator.new()
	rng.seed = SEED
	for i in SPAWN_COUNT:
		spawned.append(spawn_source("Spawned%d" % i, DIM.random_reverb_room_point(rng), rng.randi()))

	for source in spawned:
		await wait_raytraced_by_listener(source)

	# Sample a few times while the crowd plays, since grouping is redone every pass
	var peak := 0
	for i in 5:
		await wait_raytraced(SETTLE_PASSES)
		var count := VA.grouped_eax_count(root.world)
		peak = maxi(peak, count)
		check(count <= maximum, "grouped EAX count %d with %d sources exceeds maximum_grouped_eax_count %d" % [count, SPAWN_COUNT + 1, maximum])
		for source in spawned:
			var index := VA.grouped_eax_index(source)
			check(index < maximum, "%s has grouped EAX index %d, expected < maximum_grouped_eax_count %d" % [source.name, index, maximum])
	await step("crowd of %d voices" % SPAWN_COUNT, 2.0)
	print("[devproject] %d sources spawned -> peak grouped EAX count %d" % [SPAWN_COUNT, peak])

	free_spawned()
	await step("all spawned sources freed - expect only the original voice", 1.0)
	var freed_at := Time.get_ticks_msec()
	var after := await wait_grouped_eax_count(baseline)
	print("[devproject] spawned sources freed -> grouped EAX count %d after %.2f s" % [after, (Time.get_ticks_msec() - freed_at) / 1000.0])

	check(after == baseline, "grouped EAX count %d after freeing the spawned sources didn't return to the baseline %d" % [after, baseline])
	check(VA.is_raytraced_by_listener(root.source), "original source is no longer raytraced after freeing the spawned sources")
	check(VA.grouped_eax_index(root.source) >= 0, "original source isn't grouped after freeing the spawned sources")

func free_spawned() -> void:
	for source in spawned:
		if is_instance_valid(source):
			source.queue_free()
	spawned.clear()

func teardown() -> void:
	free_spawned()
	await wait_raytraced()
