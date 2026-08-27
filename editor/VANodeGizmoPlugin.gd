@tool
extends EditorNode3DGizmoPlugin

# Draws a small wireframe sphere in the 3D viewport at every VAEmitter/VAListener/VASource/
# VAStreamSource (and their subclasses, e.g. VAInputStreamSource, VANetworkedStreamSource), so
# they're visible while editing a scene even though they have no mesh of their own. Editor-only -
# gizmos never render in the running game.

const VAEmitter = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VAEmitter.cs")
const VASource = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VASource.cs")
const VAStreamSource = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VAStreamSource.cs")

# Brand green, matching icons/vercidium.svg's fill.
const GIZMO_COLOR = Color("85ffa4")

const SPHERE_RADIUS = 0.35
const SPHERE_SEGMENTS = 24

func _init():
	create_material("va_node", GIZMO_COLOR)

func _has_gizmo(for_node_3d):
	# VAListener is a VAEmitter subclass; VASource/VAStreamSource cover their own subclasses
	# (VAInputStreamSource, VANetworkedStreamSource) - so these three checks catch all four
	# requested node types and anything derived from them.
	return for_node_3d is VAEmitter or for_node_3d is VASource or for_node_3d is VAStreamSource

func _get_gizmo_name():
	return "VANode"

func _redraw(gizmo: EditorNode3DGizmo):
	gizmo.clear()

	var material = get_material("va_node", gizmo)

	# Three orthogonal rings - the cheapest recognisable "sphere" wireframe, same trick Godot's
	# own OmniLight3D / AudioStreamPlayer3D gizmos use.
	var lines = PackedVector3Array()
	for ring in 3:
		for i in SPHERE_SEGMENTS:
			var a = float(i) / SPHERE_SEGMENTS * TAU
			var b = float(i + 1) / SPHERE_SEGMENTS * TAU
			var p_a = _ring_point(ring, a)
			var p_b = _ring_point(ring, b)
			lines.push_back(p_a)
			lines.push_back(p_b)

	gizmo.add_lines(lines, material)

func _ring_point(ring: int, angle: float) -> Vector3:
	var c = cos(angle) * SPHERE_RADIUS
	var s = sin(angle) * SPHERE_RADIUS
	match ring:
		0: return Vector3(c, s, 0.0)   # XY plane
		1: return Vector3(c, 0.0, s)   # XZ plane
		_: return Vector3(0.0, c, s)   # YZ plane
