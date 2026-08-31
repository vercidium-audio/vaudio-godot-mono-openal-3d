@tool
extends EditorInspectorPlugin

var debugger_plugin

var _current_object

func _can_handle(object) -> bool:
	_current_object = object
	return object is VADefaultMaterial or object is VACustomMaterial

func set_debugger_plugin(plugin) -> void:
	debugger_plugin = plugin

	var inspector := EditorInterface.get_inspector()
	if inspector != null and not inspector.property_edited.is_connected(_on_property_edited):
		inspector.property_edited.connect(_on_property_edited)

func _on_property_edited(property_name: String) -> void:
	var node := _current_object as Node
	if node == null:
		return

	if not (node is VADefaultMaterial or node is VACustomMaterial):
		return

	_sync_running_game(node)

func _sync_running_game(node: Node) -> void:
	if debugger_plugin == null:
		return

	var scene_root := EditorInterface.get_edited_scene_root()
	if scene_root == null:
		return

	var is_custom_material := node is VACustomMaterial

	var material_type: int = 0 if is_custom_material else node.MaterialType
	var custom_material_name: String = node.MaterialName if is_custom_material else ""

	var node_path := scene_root.get_path_to(node)
	debugger_plugin.sync_material_properties(scene_root.name, node_path, node.name,
		is_custom_material, material_type, custom_material_name, node.AbsorptionLF,
		node.AbsorptionHF, node.Scattering, node.TransmissionLF, node.TransmissionHF,
		node.FlatTransmissionLF, node.FlatTransmissionHF, node.DebugColor)
