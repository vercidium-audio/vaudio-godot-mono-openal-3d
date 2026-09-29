extends "res://scenarios/lib/scenario.gd"

# An unknown material name must log a warning and fall back to Air (no primitive), not error or crash. Covers both places a name is resolved - a primitive added at runtime, and an existing one re-read via sync_primitive after its metadata changed

const MISSING_ON_ADD := "godot_test_missing_on_add"
const MISSING_ON_SYNC := "godot_test_missing_on_sync"

var partition: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	var open := await measure("no partition - expect clear speech")

	expect_warning(warning_for(MISSING_ON_ADD))
	partition = DIM.add_reverb_room_partition(root, MISSING_ON_ADD)
	var added := await measure("partition with an unknown material - expect a warning, and speech to stay clear")

	partition.set_meta(DIM.MATERIAL_META, "brick")
	VA.sync_primitive(root.world, partition)
	var brick := await measure("partition switched to brick - expect heavily muffled speech")

	expect_warning(warning_for(MISSING_ON_SYNC))
	partition.set_meta(DIM.MATERIAL_META, MISSING_ON_SYNC)
	VA.sync_primitive(root.world, partition)
	var synced := await measure("partition switched to an unknown material - expect a warning, and clear speech again")

	for r in [["no partition", open], ["unknown on add", added], ["brick", brick], ["unknown on sync", synced]]:
		print("[devproject] %s -> muffling LF %.4f HF %.4f" % [r[0], r[1].x, r[1].y])

	check(is_same(added, open), "partition with an unknown material wasn't treated as Air (LF %.4f vs %.4f open, HF %.4f vs %.4f)" % [added.x, open.x, added.y, open.y])
	check(brick.x < open.x - 0.5, "brick partition didn't muffle (LF %.4f vs %.4f open), so the Air checks prove nothing" % [brick.x, open.x])
	check(is_same(synced, open), "partition switched to an unknown material wasn't treated as Air (LF %.4f vs %.4f open, HF %.4f vs %.4f)" % [synced.x, open.x, synced.y, open.y])

# Both plugins log "Unknown material for node <name>: <material>. Defaulting to Air" - the node name is auto-generated, so match from the material onwards
func warning_for(material: String) -> String:
	return ": %s. Defaulting to Air" % material

func is_same(a: Vector2, b: Vector2) -> bool:
	return absf(a.x - b.x) < 0.02 and absf(a.y - b.y) < 0.02

# Returns (muffling LF, muffling HF)
func measure(banner: String) -> Vector2:
	await wait_raytraced(SETTLE_PASSES)
	await step(banner, 1.0)
	return Vector2(VA.muffling_lf(root.source), VA.muffling_hf(root.source))

func teardown() -> void:
	if partition:
		partition.queue_free()
	await wait_raytraced()
