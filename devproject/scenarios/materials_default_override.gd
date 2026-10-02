extends "res://scenarios/lib/scenario.gd"

# Adds a VADefaultMaterial for concrete at runtime, then frees it without touching its properties - the world must go back to concrete's built-in values. Uses a sealed concrete partition in the reverb room, so concrete's transmission is the only thing deciding the muffling

const CONCRETE := 3

# Wide open, so the override flips the partition from blocking to see-through
const OVERRIDE_TRANSMISSION := 30.0

var partition: Node
var override: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)
	partition = DIM.add_reverb_room_partition(root, "concrete")

func run() -> void:
	var baseline := await measure("concrete partition with built-in values - expect heavily muffled speech")

	override = VA.create_node(root.world, "VADefaultMaterial")
	VA.set_value(override, "material_type", CONCRETE)
	VA.set_value(override, "transmission_lf", OVERRIDE_TRANSMISSION)
	VA.set_value(override, "transmission_hf", OVERRIDE_TRANSMISSION)
	root.world.add_child(override)
	var overridden := await measure("VADefaultMaterial makes concrete transparent - expect clear speech through the partition")

	override.queue_free()
	override = null
	var restored := await measure("VADefaultMaterial freed - expect heavily muffled speech again")

	for r in [["baseline", baseline], ["override", overridden], ["removed", restored]]:
		print("[devproject] %s -> muffling LF %.4f HF %.4f" % [r[0], r[1].x, r[1].y])

	check(overridden.x > baseline.x + 0.5 and overridden.y > baseline.y + 0.5, "override didn't unmuffle the partition (LF %.4f -> %.4f, HF %.4f -> %.4f)" % [baseline.x, overridden.x, baseline.y, overridden.y])
	check(absf(restored.x - baseline.x) < 0.02 and absf(restored.y - baseline.y) < 0.02, "freeing the override didn't restore concrete's built-in values (LF %.4f vs baseline %.4f, HF %.4f vs %.4f)" % [restored.x, baseline.x, restored.y, baseline.y])

# Returns (muffling LF, muffling HF)
func measure(banner: String) -> Vector2:
	await wait_raytraced(SETTLE_PASSES)
	await step(banner, 1.0)
	return Vector2(VA.muffling_lf(root.source), VA.muffling_hf(root.source))

func teardown() -> void:
	if override:
		override.queue_free()
	partition.queue_free()
	await wait_raytraced()
