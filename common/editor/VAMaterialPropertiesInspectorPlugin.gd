@tool
extends EditorInspectorPlugin

# VADefaultMaterial/VACustomMaterial's [Export] property setters (AbsorptionLF, Scattering, etc.)
# only push their new value into a vaudio.World when vercidiumAudio/vaudioMaterial is non-null,
# which is only ever assigned in their own _EnterTree - and that bails out immediately when
# Engine.IsEditorHint() is true (see nodes/VADefaultMaterial.cs, nodes/VACustomMaterial.cs). So
# editing one of these nodes' properties in the Inspector while the game is running has no effect
# on the actual running raytracing world, exactly like the material-assignment dropdown this
# mirrors (see VAMaterialInspectorPlugin.gd's header comment for the fuller rationale). Unlike that
# dropdown, these are plain Godot-drawn float/color editors rather than a custom control, so there
# is no per-control "selected" signal to hook - instead this connects once to the shared
# EditorInspector's property_edited signal and filters by the currently edited object's type.
#
# Shared verbatim between the 2D and 3D addons via each addon's `common` symlink -
# VADefaultMaterial/VACustomMaterial are [GlobalClass] so they resolve by name without a preload().

# Relays edits to a running game's VAWorld - see VADebuggerPlugin. Null until plugin_main.gd
# finishes wiring it up via set_debugger_plugin.
var debugger_plugin

# The object currently being drawn by this plugin pass - _can_handle runs once per inspected
# object right before Godot asks this plugin to parse its properties, so it doubles as the
# "which object is the property_edited signal about" filter without needing EditorInterface to
# expose that directly.
var _current_object

func _can_handle(object) -> bool:
	_current_object = object
	return object is VADefaultMaterial or object is VACustomMaterial

func set_debugger_plugin(plugin) -> void:
	debugger_plugin = plugin

	var inspector := EditorInterface.get_inspector()
	if inspector != null and not inspector.property_edited.is_connected(_on_property_edited):
		inspector.property_edited.connect(_on_property_edited)

# property_edited only reports the edited property's name, not which object it belongs to -
# _current_object (set by _can_handle just before this plugin drew that object's properties) is
# used as a best-effort match. Fires for every Inspector edit in the editor, not just our own node
# types, so the _can_handle-style type check has to be repeated here.
func _on_property_edited(property_name: String) -> void:
	var node := _current_object as Node
	if node == null:
		return

	if not (node is VADefaultMaterial or node is VACustomMaterial):
		return

	_sync_running_game(node)

# Mirrors VAMaterialInspectorPlugin._sync_running_game - see its header comment for why the
# debugger protocol is the only bridge between the editor's local copy of this node and the
# running game's separate copy.
#
# node.name/is_custom_material/material_type/custom_material_name let the receiving end
# (VAWorldDebugger.cs) create the node itself if it doesn't exist in the running game yet - e.g. a
# VADefaultMaterial/VACustomMaterial added in the editor while the game is already running never
# enters the running game's own scene tree (the debugger protocol only relays property edits, not
# new nodes), so without this the sync would just fail with "no matching node exists".
func _sync_running_game(node: Node) -> void:
	if debugger_plugin == null:
		return

	var scene_root := EditorInterface.get_edited_scene_root()
	if scene_root == null:
		return

	var is_custom_material := node is VACustomMaterial

	# VADefaultMaterial.MaterialType (a C# enum) marshals to GDScript as a plain int - VACustomMaterial
	# has no such property, so 0 is sent here and the receiving end only reads it for VADefaultMaterial.
	var material_type: int = 0 if is_custom_material else node.MaterialType
	var custom_material_name: String = node.MaterialName if is_custom_material else ""

	var node_path := scene_root.get_path_to(node)
	debugger_plugin.sync_material_properties(scene_root.name, node_path, node.name,
		is_custom_material, material_type, custom_material_name, node.AbsorptionLF,
		node.AbsorptionHF, node.Scattering, node.TransmissionLF, node.TransmissionHF,
		node.FlatTransmissionLF, node.FlatTransmissionHF, node.DebugColor)
