extends "res://scenarios/lib/scenario.gd"

# Spawns a tight group of sources behind a partly transmissive brick partition, then turns on clustering (sources share rays) and target clustering (the listener groups them). Clustered sources must end up with identical muffling and the same grouped EAX slot

const BRICK := 1
const GROUP_SIZE := 4
const SEED := 20261027

var override: Node
var partition: Node
var spawned: Array[Node] = []
var original_target_clustering: bool
var original_target_distance: float

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)
	original_target_clustering = VA.get_value(root.listener, "target_clustering_enabled")
	original_target_distance = VA.get_value(root.listener, "target_clustering_distance")

	# HF transmission of twice the wall thickness leaves the group partly muffled, so the gains aren't all capped at 1
	override = VA.create_node(root.world, "VADefaultMaterial")
	VA.set_value(override, "material_type", BRICK)
	VA.set_value(override, "transmission_lf", 30.0)
	VA.set_value(override, "transmission_hf", DIM.PARTITION_THICKNESS_METRES * 2.0)
	root.world.add_child(override)
	partition = DIM.add_reverb_room_partition(root, "brick")

	var rng := RandomNumberGenerator.new()
	rng.seed = SEED
	for i in GROUP_SIZE:
		spawned.append(spawn_source("Clustered%d" % i, DIM.reverb_room_cluster_point(i), rng.randi()))
	for source in spawned:
		await wait_raytraced_by_listener(source)

func run() -> void:
	check_round_trip()

	var unclustered := await measure_group("clustering off", "group of %d voices behind the wall, each traced on its own" % GROUP_SIZE)
	print("[devproject] clustering off -> HF spread %.4f" % hf_spread(unclustered))

	set_clustering(true)
	var clustered := await measure_group("clustering on", "same group, clustered - expect no audible change")

	for i in range(1, GROUP_SIZE):
		check(clustered[i] == clustered[0], "%s muffling (%.4f, %.4f) differs from %s (%.4f, %.4f) with target clustering on" % [spawned[i].name, clustered[i].x, clustered[i].y, spawned[0].name, clustered[0].x, clustered[0].y])

	var index := VA.grouped_eax_index(spawned[0])
	check(index >= 0, "%s isn't grouped with clustering on" % spawned[0].name)
	for source in spawned:
		check(VA.grouped_eax_index(source) == index, "%s has grouped EAX index %d, expected the cluster's %d" % [source.name, VA.grouped_eax_index(source), index])

	# Turning clustering back off must hand each source its own results again without errors
	set_clustering(false)
	var restored := await measure_group("clustering off again", "group traced on its own again")
	for gains in restored:
		check(gains.x >= 0.0 and gains.x <= 1.0 and gains.y >= 0.0 and gains.y <= 1.0, "muffling (%.4f, %.4f) out of range after turning clustering off" % [gains.x, gains.y])

# Distances are clamped above 0 by the node, so an invalid value never reaches the SDK
func check_round_trip() -> void:
	var source := spawned[0]
	VA.set_value(source, "clustering_enabled", true)
	check(VA.get_value(source, "clustering_enabled") == true, "clustering_enabled didn't round-trip")
	VA.set_value(source, "clustering_distance", 0.0)
	check(VA.get_value(source, "clustering_distance") > 0.0, "clustering_distance 0 wasn't clamped above 0")
	VA.set_value(source, "clustering_enabled", false)

	VA.set_value(root.listener, "target_clustering_distance", -1.0)
	check(VA.get_value(root.listener, "target_clustering_distance") > 0.0, "target_clustering_distance -1 wasn't clamped above 0")

func set_clustering(enabled: bool) -> void:
	for source in spawned:
		VA.set_value(source, "clustering_enabled", enabled)
		VA.set_value(source, "clustering_distance", DIM.CLUSTER_DISTANCE)
	VA.set_value(root.listener, "target_clustering_enabled", enabled)
	VA.set_value(root.listener, "target_clustering_distance", DIM.CLUSTER_DISTANCE)

func measure_group(label: String, banner: String) -> Array[Vector2]:
	await wait_raytraced(SETTLE_PASSES)
	await step(banner, 1.0)
	var gains: Array[Vector2] = []
	for source in spawned:
		var muffling := Vector2(VA.muffling_lf(source), VA.muffling_hf(source))
		print("[devproject] %s, %s -> muffling LF %.4f HF %.4f, grouped EAX index %d" % [label, source.name, muffling.x, muffling.y, VA.grouped_eax_index(source)])
		gains.append(muffling)
	return gains

func hf_spread(gains: Array[Vector2]) -> float:
	var lowest := INF
	var highest := -INF
	for g in gains:
		lowest = minf(lowest, g.y)
		highest = maxf(highest, g.y)
	return highest - lowest

func teardown() -> void:
	VA.set_value(root.listener, "target_clustering_enabled", original_target_clustering)
	VA.set_value(root.listener, "target_clustering_distance", original_target_distance)
	for source in spawned:
		if is_instance_valid(source):
			source.queue_free()
	spawned.clear()
	partition.queue_free()
	override.queue_free()
	await wait_raytraced()
