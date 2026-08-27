@tool
extends EditorNode3DGizmoPlugin

# Draws a solid shaded sphere in the 3D viewport at every VAEmitter/VAListener/VASource/
# VAStreamSource (and their subclasses, e.g. VAInputStreamSource, VANetworkedStreamSource), so
# they're visible while editing a scene even though they have no mesh of their own. Editor-only -
# gizmos never render in the running game.
#
# A gizmo mesh can't be a real CSGSphere3D/MeshInstance3D node (gizmos only add draw primitives,
# not scene nodes), but a lit StandardMaterial3D on the mesh gives it the same solid, shaded,
# depth-occluding look - it catches the editor's scene lighting and hides geometry behind it just
# like a real object would.

const VAEmitter = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VAEmitter.cs")
const VASource = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VASource.cs")
const VAStreamSource = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VAStreamSource.cs")

# Brand green, matching icons/vercidium.svg's fill.
const GIZMO_COLOR = Color("85ffa4")

const SPHERE_RADIUS = 0.5

# Built once and reused for every gizmo redraw - a SphereMesh is immutable here so there's no
# reason to rebuild it per node.
var _sphere_mesh: SphereMesh

func _init():
	_sphere_mesh = SphereMesh.new()
	_sphere_mesh.radius = SPHERE_RADIUS
	_sphere_mesh.height = SPHERE_RADIUS * 2.0
	_sphere_mesh.radial_segments = 32
	_sphere_mesh.rings = 16

	# Fully lit (SHADING_MODE_PER_PIXEL, the default) and opaque, so it reads as a real solid
	# object: it takes the scene's lighting and occludes geometry behind it. A little emission so
	# it stays visible even in an unlit / dark scene.
	var fill := StandardMaterial3D.new()
	fill.albedo_color = GIZMO_COLOR
	fill.roughness = 0.55
	fill.metallic = 0.0
	fill.emission_enabled = true
	fill.emission = GIZMO_COLOR
	fill.emission_energy_multiplier = 0.35
	add_material("va_node_fill", fill)

func _has_gizmo(for_node_3d):
	# VAListener is a VAEmitter subclass; VASource/VAStreamSource cover their own subclasses
	# (VAInputStreamSource, VANetworkedStreamSource) - so these three checks catch all four
	# requested node types and anything derived from them.
	return for_node_3d is VAEmitter or for_node_3d is VASource or for_node_3d is VAStreamSource

func _get_gizmo_name():
	return "VANode"

func _redraw(gizmo: EditorNode3DGizmo):
	gizmo.clear()
	gizmo.add_mesh(_sphere_mesh, get_material("va_node_fill", gizmo))

	# add_mesh is visual only - the editor won't click-select a node by its gizmo unless the
	# gizmo also contributes collision geometry the editor can raycast against. SphereMesh builds
	# its own TriangleMesh (same faces as the drawn mesh), which is exactly what
	# add_collision_triangles wants.
	var tri_mesh := _sphere_mesh.generate_triangle_mesh()
	if tri_mesh:
		gizmo.add_collision_triangles(tri_mesh)
