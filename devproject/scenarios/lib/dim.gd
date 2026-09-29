extends RefCounted

# 3D geometry helpers - the 2D version has the same functions, so scenarios using them stay dimension-agnostic

const MATERIAL_META := "vercidium_audio_material"

# Metadata must be set before the node enters the tree, since that's when the plugin creates its primitive
static func add_box(parent: Node, center: Vector3, size: Vector3, material: String) -> Node:
	var mesh := BoxMesh.new()
	mesh.size = size
	var box := MeshInstance3D.new()
	box.mesh = mesh
	box.position = center
	box.set_meta(MATERIAL_META, material)
	parent.add_child(box)
	return box

const PARTITION_THICKNESS_METRES := 0.3

# Floor-to-ceiling, wall-to-wall divider across the middle of reverb_room.tscn, between the listener (x = -3) and source (x = 3)
static func add_reverb_room_partition(root: Node, material: String) -> Node:
	return add_box(root, Vector3(0, 3, 0), Vector3(PARTITION_THICKNESS_METRES, 7, 9), material)
