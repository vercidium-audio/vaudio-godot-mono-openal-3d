extends "res://scenarios/lib/scenario.gd"

# Resizes a sealed CSGBox3D partition and toggles a child CSG shape's operation. Node property edits have no change signal, so they're applied with VAWorld.sync_primitive, same as metadata changes

const SEALED := Vector3(DIM.PARTITION_THICKNESS_METRES, 7, 9)
const NARROW := Vector3(DIM.PARTITION_THICKNESS_METRES, 7, 1)

var partition: CSGBox3D

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	partition = CSGBox3D.new()
	partition.size = SEALED
	partition.position = Vector3(0, 3, 0)
	partition.set_meta(DIM.MATERIAL_META, "brick")
	root.add_child(partition)
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	var sealed := await measure_muffling("sealed", "CSG partition sealing the room - expect muffled speech")
	await resize(NARROW)
	var narrow := await measure_muffling("narrowed", "CSG partition narrowed to 1 m - expect clear speech")
	await resize(SEALED)
	var resealed := await measure_muffling("resealed", "CSG partition resized to seal the room again - expect muffled speech")

	# vaudio makes one primitive per CSG node and doesn't evaluate CSG operations, so a subtraction child adds solid geometry rather than cutting a hole. Only logged, this pins down that toggling it doesn't error
	var doorway := CSGBox3D.new()
	doorway.size = Vector3(1, 2, 1)
	doorway.position = Vector3(0, -2, 1.5)
	partition.add_child(doorway)
	await measure_muffling("union child", "union child added - expect muffled speech")
	doorway.operation = CSGShape3D.OPERATION_SUBTRACTION
	VA.sync_primitive(root.world, partition)
	await measure_muffling("subtraction child", "child switched to subtraction - Godot cuts a doorway, vaudio still treats it as solid")
	doorway.queue_free()

	check(sealed.y < 0.1, "muffling HF %.4f with the CSG partition sealing the room, expected heavily muffled" % sealed.y)
	check(narrow.y > 0.9, "muffling HF %.4f after narrowing the CSG partition, expected almost unmuffled" % narrow.y)
	check(absf(resealed.y - sealed.y) < 0.02, "muffling HF %.4f after resealing didn't return to %.4f" % [resealed.y, sealed.y])

func resize(size: Vector3) -> void:
	partition.size = size
	VA.sync_primitive(root.world, partition)

func teardown() -> void:
	partition.queue_free()
	await wait_raytraced()
