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

# Listener spot in reverb_room.tscn on the source's side of the partition, 3 m from the source (x = 3, z = 1.5) with clear line of sight
const REVERB_ROOM_LOS_LISTENER := Vector3(3, 1, -1.5)

# x for a partition between the source (x = 3) and the right wall, so the listener and source are on the same side of it
const REVERB_ROOM_BEYOND_SOURCE_X := 4.5

# Listener spot with the same z as the source - a long partition turned to run along x leaves them both on the same side
const REVERB_ROOM_SAME_SIDE_LISTENER := Vector3(-3, 1, 1.5)

# Divider long enough to seal reverb_room.tscn along either horizontal axis, so a quarter turn moves it from between the listener and source to beside them
static func add_reverb_room_long_partition(root: Node, material: String) -> Node:
	return add_box(root, Vector3(0, 3, 0), Vector3(PARTITION_THICKNESS_METRES, 7, 13), material)

# Random point inside reverb_room.tscn, kept 0.5 m clear of the walls, floor and ceiling
static func random_reverb_room_point(rng: RandomNumberGenerator) -> Vector3:
	return Vector3(rng.randf_range(-5.5, 5.5), rng.randf_range(0.5, 5.5), rng.randf_range(-3.5, 3.5))

static func turn(node: Node, quarter_turns: int) -> void:
	node.rotation.y = quarter_turns * PI * 0.5

# Scales a partition along its length, e.g. 0.05 leaves a narrow column that sound goes around
static func squash(node: Node, factor: float) -> void:
	node.scale = Vector3(1, 1, factor)

static func make_body(center: Vector3, size: Vector3, collision_layer: int) -> StaticBody3D:
	var shape := BoxShape3D.new()
	shape.size = size
	var collider := CollisionShape3D.new()
	collider.shape = shape
	var body := StaticBody3D.new()
	body.position = center
	body.collision_layer = collision_layer
	body.add_child(collider)
	return body

# Wall-to-wall divider built from StaticBody3D pieces, with the material only on their shared parent. The subtree is built off-tree and added at once like an instanced scene, so each collider has to inherit the material from two levels up
static func add_reverb_room_body_partition(root: Node, material: String, pieces := 20, collision_layer := 1) -> Node:
	var container := Node3D.new()
	container.set_meta(MATERIAL_META, material)
	var depth := 9.0 / pieces
	for i in pieces:
		container.add_child(make_body(Vector3(0, 3, -4.5 + depth * (i + 0.5)), Vector3(PARTITION_THICKNESS_METRES, 7, depth + 0.05), collision_layer))
	root.add_child(container)
	return container

static func colliders(node: Node) -> Array[Node]:
	return node.find_children("*", "CollisionShape3D", true, false)

# Empty spatial node for moving or reparenting other nodes under
static func add_pivot(parent: Node, position: Vector3) -> Node:
	var pivot := Node3D.new()
	pivot.position = position
	parent.add_child(pivot)
	return pivot
