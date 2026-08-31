@tool
extends EditorNode3DGizmoPlugin

const VAEmitter = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VAEmitter.cs")
const VASource = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VASource.cs")
const VAStreamSource = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VAStreamSource.cs")

const GIZMO_COLOR = Color("85ffa4")
const SPHERE_RADIUS = 0.5

var _sphere_mesh: SphereMesh

func _init():
	_sphere_mesh = SphereMesh.new()
	_sphere_mesh.radius = SPHERE_RADIUS
	_sphere_mesh.height = SPHERE_RADIUS * 2.0
	_sphere_mesh.radial_segments = 32
	_sphere_mesh.rings = 16

	var fill := StandardMaterial3D.new()
	fill.albedo_color = GIZMO_COLOR
	fill.roughness = 0.55
	fill.metallic = 0.0
	fill.emission_enabled = true
	fill.emission = GIZMO_COLOR
	fill.emission_energy_multiplier = 0.35
	add_material("va_node_fill", fill)

func _has_gizmo(for_node_3d):
	return for_node_3d is VAEmitter or for_node_3d is VASource or for_node_3d is VAStreamSource

func _get_gizmo_name():
	return "VANode"

func _redraw(gizmo: EditorNode3DGizmo):
	gizmo.clear()
	gizmo.add_mesh(_sphere_mesh, get_material("va_node_fill", gizmo))

	var tri_mesh := _sphere_mesh.generate_triangle_mesh()
	if tri_mesh:
		gizmo.add_collision_triangles(tri_mesh)
