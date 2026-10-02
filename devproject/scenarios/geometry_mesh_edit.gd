extends "res://scenarios/lib/scenario.gd"

# Swaps a sealed MeshInstance3D partition's mesh for a narrow one and back. MeshInstance3D has no signal for a mesh swap, so it's applied with VAWorld.sync_primitive, same as metadata changes

var partition: MeshInstance3D

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	partition = DIM.add_reverb_room_partition(root, "brick")
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	var sealed_mesh := partition.mesh
	var narrow_mesh := BoxMesh.new()
	narrow_mesh.size = Vector3(DIM.PARTITION_THICKNESS_METRES, 7, 1)

	var sealed := await measure_muffling("sealed mesh", "partition mesh sealing the room - expect muffled speech")
	swap(narrow_mesh)
	var narrow := await measure_muffling("narrow mesh", "partition mesh swapped for a 1 m wide one - expect clear speech")
	swap(sealed_mesh)
	var resealed := await measure_muffling("sealed mesh again", "sealing mesh swapped back in - expect muffled speech")

	check(sealed.y < 0.1, "muffling HF %.4f with the sealing mesh, expected heavily muffled" % sealed.y)
	check(narrow.y > 0.9, "muffling HF %.4f after swapping in the narrow mesh, expected almost unmuffled" % narrow.y)
	check(absf(resealed.y - sealed.y) < 0.02, "muffling HF %.4f after swapping the sealing mesh back didn't return to %.4f" % [resealed.y, sealed.y])

func swap(mesh: Mesh) -> void:
	partition.mesh = mesh
	VA.sync_primitive(root.world, partition)

func teardown() -> void:
	partition.queue_free()
	await wait_raytraced()
