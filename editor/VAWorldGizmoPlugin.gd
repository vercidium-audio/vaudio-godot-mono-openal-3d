@tool
extends EditorNode3DGizmoPlugin

const VAWorld = preload("res://addons/vaudio-godot-mono-openal-3d/world/VAWorld.cs")

enum BoundsHandle { MAX_X, MAX_Y, MAX_Z, MIN_X, MIN_Y, MIN_Z }

var drag_click_offset: float

var drag_start_position: Vector3
var drag_start_size: Vector3

func _init():
	create_handle_material("handles")

func _has_gizmo(for_node_3d):
	return for_node_3d is VAWorld

func _get_gizmo_name():
	return "VAWorld"

func _redraw(gizmo: EditorNode3DGizmo):
	gizmo.clear()

	var world = gizmo.get_node_3d()
	if not (world is VAWorld):
		return

	# The node's own position is the AABB's world-space origin; rotation/scale are hidden in the
	# Inspector (see VAWorld._ValidateProperty), so the box is simply [0, Size] in local space.
	var min = Vector3.ZERO
	var max: Vector3 = world.Size

	var corners = [
		Vector3(min.x, min.y, min.z),
		Vector3(max.x, min.y, min.z),
		Vector3(max.x, min.y, max.z),
		Vector3(min.x, min.y, max.z),
		Vector3(min.x, max.y, min.z),
		Vector3(max.x, max.y, min.z),
		Vector3(max.x, max.y, max.z),
		Vector3(min.x, max.y, max.z),
	]

	var edges = [
		[0, 1], [1, 2], [2, 3], [3, 0], # bottom face
		[4, 5], [5, 6], [6, 7], [7, 4], # top face
		[0, 4], [1, 5], [2, 6], [3, 7], # vertical edges
	]

	var lines = PackedVector3Array()
	for edge in edges:
		lines.push_back(corners[edge[0]])
		lines.push_back(corners[edge[1]])

	var line_color: Color = world.BoundsColor

	var line_material = StandardMaterial3D.new()
	line_material.albedo_color = line_color
	line_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	gizmo.add_lines(lines, line_material)

	var face_material = StandardMaterial3D.new()
	face_material.albedo_color = line_color
	face_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	face_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	face_material.cull_mode = BaseMaterial3D.CULL_DISABLED
	gizmo.add_mesh(_build_face_mesh(corners), face_material)

	var handles = PackedVector3Array()
	handles.push_back(Vector3(max.x, max.y * 0.5, max.z * 0.5))
	handles.push_back(Vector3(max.x * 0.5, max.y, max.z * 0.5))
	handles.push_back(Vector3(max.x * 0.5, max.y * 0.5, max.z))
	handles.push_back(Vector3(min.x, max.y * 0.5, max.z * 0.5))
	handles.push_back(Vector3(max.x * 0.5, min.y, max.z * 0.5))
	handles.push_back(Vector3(max.x * 0.5, max.y * 0.5, min.z))

	var handle_ids = PackedInt32Array()
	handle_ids.push_back(BoundsHandle.MAX_X)
	handle_ids.push_back(BoundsHandle.MAX_Y)
	handle_ids.push_back(BoundsHandle.MAX_Z)
	handle_ids.push_back(BoundsHandle.MIN_X)
	handle_ids.push_back(BoundsHandle.MIN_Y)
	handle_ids.push_back(BoundsHandle.MIN_Z)

	gizmo.add_handles(handles, get_material("handles", gizmo), handle_ids)

func _build_face_mesh(corners: Array) -> ArrayMesh:
	var faces = [
		[0, 1, 2, 3], # bottom
		[4, 7, 6, 5], # top
		[0, 4, 5, 1], # -z side
		[1, 5, 6, 2], # +x side
		[2, 6, 7, 3], # +z side
		[3, 7, 4, 0], # -x side
	]

	var st = SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)

	for face in faces:
		var a: Vector3 = corners[face[0]]
		var b: Vector3 = corners[face[1]]
		var c: Vector3 = corners[face[2]]
		var d: Vector3 = corners[face[3]]

		st.add_vertex(a)
		st.add_vertex(b)
		st.add_vertex(c)

		st.add_vertex(a)
		st.add_vertex(c)
		st.add_vertex(d)

	return st.commit()

func _get_handle_name(gizmo: EditorNode3DGizmo, handle_id: int, secondary: bool):
	match handle_id:
		BoundsHandle.MAX_X: return "Size +X"
		BoundsHandle.MAX_Y: return "Size +Y"
		BoundsHandle.MAX_Z: return "Size +Z"
		BoundsHandle.MIN_X: return "Size -X"
		BoundsHandle.MIN_Y: return "Size -Y"
		BoundsHandle.MIN_Z: return "Size -Z"
		_: return ""

func _get_handle_value(gizmo: EditorNode3DGizmo, handle_id: int, secondary: bool):
	var world = gizmo.get_node_3d()
	if not (world is VAWorld):
		return null

	return [world.position, world.Size]

func _begin_handle_action(gizmo: EditorNode3DGizmo, handle_id: int, secondary: bool):
	drag_click_offset = INF

	var world = gizmo.get_node_3d()
	if world is VAWorld:
		drag_start_position = world.position
		drag_start_size = world.Size

func _set_handle(gizmo: EditorNode3DGizmo, handle_id: int, secondary: bool, camera: Camera3D, screen_pos: Vector2):
	var world = gizmo.get_node_3d()
	if not (world is VAWorld):
		return

	var axis: int
	var is_min_face: bool
	match handle_id:
		BoundsHandle.MAX_X: axis = Vector3.AXIS_X; is_min_face = false
		BoundsHandle.MAX_Y: axis = Vector3.AXIS_Y; is_min_face = false
		BoundsHandle.MAX_Z: axis = Vector3.AXIS_Z; is_min_face = false
		BoundsHandle.MIN_X: axis = Vector3.AXIS_X; is_min_face = true
		BoundsHandle.MIN_Y: axis = Vector3.AXIS_Y; is_min_face = true
		BoundsHandle.MIN_Z: axis = Vector3.AXIS_Z; is_min_face = true
		_: return

	var parent = world.get_parent_node_3d()
	var parent_transform: Transform3D = parent.global_transform if parent else Transform3D.IDENTITY
	var axis_origin = parent_transform * drag_start_position
	var axis_direction = parent_transform.basis[axis].normalized()

	var handle_offset: Vector3 = drag_start_size
	handle_offset[axis] = 0.0 if is_min_face else drag_start_size[axis]
	var handle_position = axis_origin + parent_transform.basis * (handle_offset * 0.5)

	var ray_origin = camera.project_ray_origin(screen_pos)
	var ray_direction = camera.project_ray_normal(screen_pos)

	var view_direction = (handle_position - camera.global_transform.origin)
	var plane_normal = axis_direction.cross(view_direction.cross(axis_direction))
	var plane_normal_length = plane_normal.length()

	if plane_normal_length < 0.0001 or absf(plane_normal.normalized().dot(ray_direction)) < 0.0001:
		return

	plane_normal /= plane_normal_length

	var t = (handle_position - ray_origin).dot(plane_normal) / ray_direction.dot(plane_normal)
	var hit_point = ray_origin + ray_direction * t

	var new_length = (hit_point - axis_origin).dot(axis_direction)

	if is_inf(drag_click_offset):
		var start_face_length = 0.0 if is_min_face else drag_start_size[axis]
		drag_click_offset = new_length - start_face_length

	var dragged_length = new_length - drag_click_offset

	if is_min_face:
		var max_face_length = drag_start_size[axis]
		var new_min = min(dragged_length, max_face_length)

		var position: Vector3 = drag_start_position
		position[axis] += new_min
		world.position = position

		var size: Vector3 = world.Size
		size[axis] = max_face_length - new_min
		world.Size = size
	else:
		var size: Vector3 = world.Size
		size[axis] = max(dragged_length, 0.0)
		world.Size = size

func _commit_handle(gizmo: EditorNode3DGizmo, handle_id: int, secondary: bool, restore, cancel: bool):
	var world = gizmo.get_node_3d()
	if not (world is VAWorld):
		return

	var restore_position: Vector3 = restore[0]
	var restore_size: Vector3 = restore[1]

	if cancel:
		world.position = restore_position
		world.Size = restore_size
		return

	var final_position: Vector3 = world.position
	var final_size: Vector3 = world.Size
	if final_position == restore_position and final_size == restore_size:
		return

	var undo_redo := EditorInterface.get_editor_undo_redo()
	undo_redo.create_action("Resize VAWorld")
	undo_redo.add_do_property(world, "position", final_position)
	undo_redo.add_do_property(world, "Size", final_size)
	undo_redo.add_undo_property(world, "position", restore_position)
	undo_redo.add_undo_property(world, "Size", restore_size)
	undo_redo.commit_action(false)
