@tool
extends EditorNode3DGizmoPlugin

# Draws a wireframe box in the 3D viewport showing VAWorld's Position/Size AABB, with draggable
# handles on all six faces so Size can be resized directly in the viewport. Dragging a +face
# handle only changes Size (the opposite -face stays put); dragging a -face handle moves Position
# on that axis while shrinking/growing Size to compensate, so the opposite +face stays fixed in
# world space.

const VAWorld = preload("res://addons/vaudio-godot-mono-openal-3d/main/VAWorld.cs")

enum BoundsHandle { MAX_X, MAX_Y, MAX_Z, MIN_X, MIN_Y, MIN_Z }

# _begin_handle_action fires exactly once per drag, at mouse-down, before any _set_handle calls -
# unlike _get_handle_value, which the editor also calls on every _set_handle frame to refresh its
# own cached "restore" value, so it can't be used as a drag-start signal (using it as one made
# drag_click_offset get recomputed fresh every frame, which is a no-op by construction and was the
# cause of the resize handles not responding to the mouse at all). Captures the offset between
# where the user actually clicked and the handle's exact on-axis position, so the box doesn't jump
# to snap under the cursor - it only tracks the cursor's movement from where the drag began. Only
# one drag can be in progress at a time, so plain instance state is sufficient.
var drag_click_offset: float

# Position/Size as of the start of the current drag (see _begin_handle_action). A -face drag
# moves Position, which would otherwise move axis_origin under the ray-cast math mid-drag (since
# it's read from the node's live global_transform) - anchoring both the ray-cast axis and the
# fixed opposite face to their pre-drag values keeps a single drag's math self-consistent frame to
# frame, the same way drag_click_offset anchors the click-to-handle offset.
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

	# Built per-instance (rather than via the plugin-wide create_material cache) since each
	# VAWorld can have its own user-set BoundsColor.
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

	# One drag handle per axis per side, at the midpoint of that face, so each handle only ever
	# resizes Size (and, for the -face handles, Position) along its own axis.
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

	# Snapshotted here and handed back to _commit_handle/_set_handle as restore, so a cancelled
	# drag (e.g. Escape) can restore the exact pre-drag Position/Size - both, since a -face drag
	# changes both together. Note this is called on every _set_handle frame (not just once at
	# drag start) to refresh the editor's own cached restore value, so drag-start detection must
	# not live here - see _begin_handle_action.
	return [world.position, world.Size]

func _begin_handle_action(gizmo: EditorNode3DGizmo, handle_id: int, secondary: bool):
	# Fires exactly once per drag, at mouse-down, before the first _set_handle call - the
	# reliable place to reset drag_click_offset and snapshot drag_start_position/drag_start_size
	# for a fresh drag (see their doc comments above).
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

	# Drag is constrained to the world-space line through the box's pre-drag min corner along the
	# chosen axis. Anchored to drag_start_position (rather than world.global_transform.origin,
	# which is live) because a -face drag moves Position every frame - if the ray math re-read the
	# live origin, the axis line itself would shift out from under the drag each frame instead of
	# staying fixed for its duration.
	#
	# Cast the mouse position into a 3D ray and intersect it with a *plane* through the handle's
	# pre-drag world position - not the closest point on the ray to the axis *line*, which is what
	# this used to do (matching the approach Gizmo3DHelper::box_set_handle appears to use at a
	# glance). That line-based approach is only exact when the handle sits on the axis itself
	# (true for CSGBox3D/CollisionShape3D, whose extent handles are at e.g. (size.x/2, 0, 0)) - ours
	# sit at face centres, off-axis (e.g. (max.x, max.y * 0.5, max.z * 0.5)), and for an off-axis
	# handle the closest-point-on-line solution advances faster than the cursor's actual on-screen
	# motion (verified numerically: a cursor moving a world-space delta of 2 units produced a
	# closest-point delta of 2.39, growing with distance from the axis and view angle) - this was
	# the "outruns the cursor" bug. Intersecting a camera-facing plane through the handle's actual
	# position instead makes the hit point track the cursor exactly 1:1 regardless of how far the
	# handle sits from the axis, because the ray always lands exactly where the cursor is pointing
	# on that plane.
	var parent = world.get_parent_node_3d()
	var parent_transform: Transform3D = parent.global_transform if parent else Transform3D.IDENTITY
	var axis_origin = parent_transform * drag_start_position
	var axis_direction = parent_transform.basis[axis].normalized()

	var handle_offset: Vector3 = drag_start_size
	handle_offset[axis] = 0.0 if is_min_face else drag_start_size[axis]
	var handle_position = axis_origin + parent_transform.basis * (handle_offset * 0.5)

	var ray_origin = camera.project_ray_origin(screen_pos)
	var ray_direction = camera.project_ray_normal(screen_pos)

	# Plane contains the axis line and is tilted to face the camera as much as possible while
	# still containing that line - the standard construction for a view-aligned axis-constrained
	# drag plane (used by e.g. Blender/Unity's axis gizmos).
	var view_direction = (handle_position - camera.global_transform.origin)
	var plane_normal = axis_direction.cross(view_direction.cross(axis_direction))
	var plane_normal_length = plane_normal.length()

	if plane_normal_length < 0.0001 or absf(plane_normal.normalized().dot(ray_direction)) < 0.0001:
		# Camera looking straight down the axis - the plane is edge-on (or the ray can't meet it),
		# so moving the mouse can't meaningfully change the position along the axis. Leave
		# Size/Position untouched this frame rather than dividing by ~zero.
		return

	plane_normal /= plane_normal_length

	var t = (handle_position - ray_origin).dot(plane_normal) / ray_direction.dot(plane_normal)
	var hit_point = ray_origin + ray_direction * t

	# new_length is world-space distance from drag_start_position along the axis. Note this makes
	# the drag speed vary with distance from the camera (the face sweeps more world-space per
	# on-screen pixel the further it recedes along the view direction) - that's expected
	# perspective foreshortening inherent to any true 3D-space axis drag, not a bug.
	var new_length = (hit_point - axis_origin).dot(axis_direction)

	# Preserve the offset between where the user clicked and the handle's exact position (rather
	# than snapping the box edge to exactly under the cursor), so the drag starts smoothly from
	# the current face position instead of jumping - see drag_click_offset's doc comment.
	# drag_click_offset is reset to INF once per drag by _begin_handle_action. For a +face handle
	# the pre-drag face position (in the same "distance from drag_start_position" units as
	# new_length) is Size[axis]; for a -face handle it's 0, since the min face starts exactly at
	# drag_start_position.
	if is_inf(drag_click_offset):
		var start_face_length = 0.0 if is_min_face else drag_start_size[axis]
		drag_click_offset = new_length - start_face_length

	var dragged_length = new_length - drag_click_offset

	if is_min_face:
		# The min face moves to track the cursor and can grow outward without limit - the only
		# constraint is it can't cross the fixed max face, which would make Size negative. new_min
		# is measured in parent-space units along the axis (same assumption Size itself already
		# makes elsewhere in this file: no parent scale).
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

	# restore is [Position, Size] as snapshotted by _get_handle_value - both are needed since a
	# -face drag changes both together.
	var restore_position: Vector3 = restore[0]
	var restore_size: Vector3 = restore[1]

	if cancel:
		world.position = restore_position
		world.Size = restore_size
		return

	# world.position/Size already hold the final dragged values (set continuously by
	# _set_handle) - so register the UndoRedo action without re-executing the "do" step
	# (commit_action(false)), matching Godot's own built-in gizmos (e.g. GPUParticles3D's
	# extents handles).
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
