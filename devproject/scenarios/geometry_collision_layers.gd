extends "res://scenarios/lib/scenario.gd"

# Excludes a static body wall's collision layer from VAWorld.collision_layers at runtime, which rebuilds every primitive. The material is inherited from the wall's parent, since a node's own material bypasses the layer filter

const WALL_LAYER := 1 << 4

var partition: Node
var original_layers: int

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	original_layers = VA.get_value(root.world, "collision_layers")
	partition = DIM.add_reverb_room_body_partition(root, "brick", 20, WALL_LAYER)
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	check(original_layers & WALL_LAYER != 0, "reverb_room.tscn's VAWorld.collision_layers %d doesn't include the wall's layer" % original_layers)

	var included := await measure_muffling("layer included", "wall's collision layer included - expect muffled speech")
	VA.set_value(root.world, "collision_layers", original_layers & ~WALL_LAYER)
	var excluded := await measure_muffling("layer excluded", "wall's collision layer excluded - expect clear speech")
	VA.set_value(root.world, "collision_layers", original_layers)
	var restored := await measure_muffling("layer restored", "wall's collision layer included again - expect muffled speech")

	check(excluded.y > 0.9, "muffling HF %.4f with the wall's layer excluded, expected almost unmuffled" % excluded.y)
	check(included.y < excluded.y * 0.5, "muffling HF %.4f with the wall's layer included isn't below half the excluded gain %.4f" % [included.y, excluded.y])
	# Not compared to the included value exactly - the rebuild re-adds every primitive in a different order, which changes how much leaks through the seams between the wall's pieces in 3D (0.12 vs 0.09)
	check(restored.y < excluded.y * 0.5, "muffling HF %.4f after restoring the layer isn't below half the excluded gain %.4f" % [restored.y, excluded.y])

func teardown() -> void:
	VA.set_value(root.world, "collision_layers", original_layers)
	partition.queue_free()
	await wait_raytraced()
