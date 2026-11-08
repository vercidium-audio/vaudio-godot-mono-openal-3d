extends "res://scenarios/lib/scenario.gd"

# Adds a solid ArrayMesh box to the reverb room, built once with a normal array and once without. Reflection rays only hit front faces, so if the mesh without normals were wound inside out, rays would pass into the box and get trapped instead of reflecting off it, changing the decay time

const BOX_CENTER := Vector3(0, 3, -2)
const BOX_SIZE := Vector3(4, 5, 3)

var box: MeshInstance3D

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	box = DIM.add_box(root, BOX_CENTER, BOX_SIZE, "concrete")
	box.mesh = box_mesh(true)
	VA.sync_primitive(root.world, box)
	var with_normals := await measure_decay("box with normals", "solid box in the room - expect a slightly shorter tail")

	box.mesh = box_mesh(false)
	VA.sync_primitive(root.world, box)
	var without_normals := await measure_decay("box without normals", "same box without a normal array - expect the same tail")

	box.mesh = box_mesh(true, true)
	VA.sync_primitive(root.world, box)
	var with_lines := await measure_decay("box with a lines surface", "same box plus a wireframe lines surface - expect the same tail")

	check(with_normals > 0.0, "source isn't in a grouped reverb with the box")
	check(absf(without_normals - with_normals) < with_normals * 0.1, "decay time %.3f with the box without normals differs from %.3f with normals, so its faces are wound inside out" % [without_normals, with_normals])
	check(absf(with_lines - with_normals) < with_normals * 0.01,"decay time %.3f with an extra lines surface differs from %.3f without it, so the lines were read as triangles" % [with_lines, with_normals])

func measure_decay(label: String, banner: String) -> float:
	await wait_raytraced(SETTLE_PASSES)
	await step(banner, 1.0)
	var decay := VA.decay_time(root.world, root.source)
	print("[devproject] %s -> decay time %.3f" % [label, decay])
	return decay

# BoxMesh's vertices and indices, optionally without its normal array, and optionally with a second PRIMITIVE_LINES surface of its 12 edges
func box_mesh(with_normals: bool, with_lines := false) -> ArrayMesh:
	var source := BoxMesh.new()
	source.size = BOX_SIZE
	var source_arrays := source.get_mesh_arrays()

	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = source_arrays[Mesh.ARRAY_VERTEX]
	arrays[Mesh.ARRAY_INDEX] = source_arrays[Mesh.ARRAY_INDEX]

	if with_normals:
		arrays[Mesh.ARRAY_NORMAL] = source_arrays[Mesh.ARRAY_NORMAL]

	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)

	if with_lines:
		var lines := []
		lines.resize(Mesh.ARRAY_MAX)
		lines[Mesh.ARRAY_VERTEX] = box_edges()
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_LINES, lines)

	return mesh

# Pairs of corners for each of the box's 12 edges, i.e. every pair of corners that differs along exactly one axis
func box_edges() -> PackedVector3Array:
	var corners := DIM.box_points(BOX_SIZE)
	var edges := PackedVector3Array()
	for i in corners.size():
		for j in range(i + 1, corners.size()):
			var diff := corners[j] - corners[i]
			if int(diff.x != 0) + int(diff.y != 0) + int(diff.z != 0) == 1:
				edges.append(corners[i])
				edges.append(corners[j])
	return edges

func teardown() -> void:
	if box != null:
		box.queue_free()
	await wait_raytraced()
