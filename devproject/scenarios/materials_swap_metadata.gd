extends "res://scenarios/lib/scenario.gd"

# Changing a primitive's material metadata at runtime only takes effect after VAWorld.sync_primitive(node) - Godot has no signal for metadata changes, and polling every node's metadata each frame isn't worth it. Uses a sealed partition in the reverb room so its material alone decides the muffling

const FROM := "brick"
const TO := "cloth"

var partition: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)
	partition = DIM.add_reverb_room_partition(root, FROM)

func run() -> void:
	var before := await measure("%s partition - expect heavily muffled speech" % FROM)

	partition.set_meta(DIM.MATERIAL_META, TO)
	var unsynced := await measure("metadata changed to %s without sync_primitive - expect no change" % TO)

	VA.sync_primitive(root.world, partition)
	var synced := await measure("sync_primitive called - expect speech through the %s partition to change" % TO)

	for r in [[FROM, before], ["%s, not synced" % TO, unsynced], ["%s, synced" % TO, synced]]:
		print("[devproject] %s -> muffling LF %.4f HF %.4f" % [r[0], r[1].x, r[1].y])

	check(absf(unsynced.x - before.x) < 0.02 and absf(unsynced.y - before.y) < 0.02, "metadata change was picked up without sync_primitive (LF %.4f -> %.4f, HF %.4f -> %.4f)" % [before.x, unsynced.x, before.y, unsynced.y])
	check(absf(synced.x - before.x) > 0.2 or absf(synced.y - before.y) > 0.2, "sync_primitive didn't apply the new material (LF %.4f -> %.4f, HF %.4f -> %.4f)" % [before.x, synced.x, before.y, synced.y])

	# Cloth lets LF through but not HF - catches sync_primitive re-adding primitives with the wrong flat transmission setting, which made the partition almost fully transparent in the C# plugin
	check(synced.y < 0.5, "cloth partition let HF %.4f through after sync_primitive, expected it to stay muffled" % synced.y)

# Returns (muffling LF, muffling HF)
func measure(banner: String) -> Vector2:
	await wait_raytraced(SETTLE_PASSES)
	await step(banner, 1.0)
	return Vector2(VA.muffling_lf(root.source), VA.muffling_hf(root.source))

func teardown() -> void:
	partition.queue_free()
	await wait_raytraced()
