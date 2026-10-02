extends "res://scenarios/lib/scenario.gd"

# Adds a VACustomMaterial at runtime and uses it on a sealed reverb-room partition, then frees it. Custom materials can't be removed at runtime, so freeing it must log an error, and primitives keep using its last values - including ones re-created afterwards via sync_primitive, which used to read the freed node in the Standard plugin

const MATERIAL_NAME := "godot_test_custom"
const REMOVED_ERROR := "Custom materials can't be removed at runtime"

# Near-opaque, so the partition only muffles if the custom material is really applied - an unresolved name falls back to Air, which lets everything through
const TRANSMISSION := 0.001

var material: Node
var partition: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)

	material = VA.create_node(root.world, "VACustomMaterial")
	VA.set_value(material, "material_name", MATERIAL_NAME)
	VA.set_value(material, "transmission_lf", TRANSMISSION)
	VA.set_value(material, "transmission_hf", TRANSMISSION)
	root.world.add_child(material)

	partition = DIM.add_reverb_room_partition(root, MATERIAL_NAME)

func run() -> void:
	var applied := await measure("partition using the runtime custom material - expect heavily muffled speech")

	expect_error(REMOVED_ERROR)
	material.queue_free()
	material = null
	var removed := await measure("custom material freed - expect an error, and speech to stay muffled")

	VA.sync_primitive(root.world, partition)
	var resynced := await measure("partition re-created after the material was freed - expect speech to stay muffled")

	for r in [["applied", applied], ["material freed", removed], ["re-created", resynced]]:
		print("[devproject] %s -> muffling LF %.4f HF %.4f" % [r[0], r[1].x, r[1].y])

	check(applied.x < 0.1 and applied.y < 0.1, "runtime custom material wasn't applied to the partition (LF %.4f HF %.4f)" % [applied.x, applied.y])
	check(absf(removed.x - applied.x) < 0.02 and absf(removed.y - applied.y) < 0.02, "freeing the custom material changed its primitives (LF %.4f -> %.4f, HF %.4f -> %.4f)" % [applied.x, removed.x, applied.y, removed.y])
	check(absf(resynced.x - applied.x) < 0.02 and absf(resynced.y - applied.y) < 0.02, "primitives re-created after freeing the custom material lost it (LF %.4f -> %.4f, HF %.4f -> %.4f)" % [applied.x, resynced.x, applied.y, resynced.y])

# Returns (muffling LF, muffling HF)
func measure(banner: String) -> Vector2:
	await wait_raytraced(SETTLE_PASSES)
	await step(banner, 1.0)
	return Vector2(VA.muffling_lf(root.source), VA.muffling_hf(root.source))

func teardown() -> void:
	partition.queue_free()
	await wait_raytraced()
