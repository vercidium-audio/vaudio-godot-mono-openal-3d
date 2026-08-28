@tool
extends EditorInspectorPlugin

# Adds a "Vercidium Audio" section to the Inspector for any spatial node (Node2D/Node3D), exposing
# the Material and Use Flat Transmission settings as an OptionButton/CheckBox rather than requiring
# the user to edit the vercidium_audio_material / vercidium_audio_use_flat_transmission metadata
# fields by hand. Both settings are inherited down the scene tree at runtime (see
# VAWorldPrimitives.AddPrimitive), so setting either on a plain node configures its whole subtree.
#
# Shared between the 2D and 3D addons via each addon's `common` symlink. Every referenced vaudio
# type is [GlobalClass] except VAWorld (registered via add_custom_type), which is load()ed relative
# to this script's own location - see _init/_addon_root.
var VAWorld: Script

func _init() -> void:
	VAWorld = load("%s/world/VAWorld.cs" % _addon_root())

# "res://addons/vaudio-godot-mono-openal-2d/common/editor/<this>.gd" -> ".../-2d"
func _addon_root() -> String:
	return get_script().resource_path.get_base_dir().get_base_dir().get_base_dir()

const MATERIAL_META_KEY = "vercidium_audio_material"
const USE_FLAT_TRANSMISSION_META_KEY = "vercidium_audio_use_flat_transmission"

# Relays edits to a running game's VAWorld - see VADebuggerPlugin. Null until plugin.gd finishes
# wiring it up via set_debugger_plugin.
var debugger_plugin

const BUILTIN_MATERIAL_NAMES = [
	"brick", "cloth", "concrete", "concretepolished", "dirt", "glass", "grass", "gravel",
	"gyprock", "ice", "leaf", "marble", "metal", "mud", "rock", "sand", "snow", "tile",
	"tree", "water", "woodindoor", "woodoutdoor",
]

func _can_handle(object):
	# Audio control/geometry-source nodes, not raytraced geometry - excluded the same way the
	# native VAMaterialInspectorPlugin excludes them. VADefaultMaterial/VACustomMaterial are plain
	# Nodes (not spatial), so this check has to happen before the Node2D/Node3D check below.
	# is_instance_of for VAWorld because it's a runtime-loaded Script var, not a parse-time type
	# (see _init) - `object is VAWorld` is a parse error for that form.
	if (is_instance_of(object, VAWorld) or object is VAEmitter or object is VADefaultMaterial
		or object is VACustomMaterial or object is VASource or object is VASourceRelative
		or object is VASourceAmbient or object is VASourceLeech):
		return false

	return object is Node2D or object is Node3D

func _parse_end(object):
	var node := object as Node
	if not (node is Node2D or node is Node3D):
		return

	var section := VBoxContainer.new()
	section.add_child(_make_heading())

	section.add_child(_make_material_row(node))
	section.add_child(_make_use_flat_transmission_row(node))

	add_custom_control(section)

func _make_heading() -> Label:
	var label := Label.new()
	label.text = "Vercidium Audio"

	# Raw pixel font-size overrides aren't multiplied by the editor's UI scale (unlike the
	# built-in theme), so on high OS/editor scale factors this rendered tiny - scale it manually.
	label.add_theme_font_size_override("font_size", roundi(14 * EditorInterface.get_editor_scale()))
	return label

func _make_material_row(node: Node) -> HBoxContainer:
	var row := HBoxContainer.new()

	var label := Label.new()
	label.text = "Material"
	label.custom_minimum_size.x = 120 * EditorInterface.get_editor_scale()
	row.add_child(label)

	var option_button := OptionButton.new()
	option_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL

	# Index 0 ("None") means "no material" - not stored as metadata, removes it instead.
	# Index 1 ("Air") explicitly stores the "air" material metadata string.
	option_button.add_item("None")
	option_button.add_item("Air")

	for material_name in BUILTIN_MATERIAL_NAMES:
		option_button.add_item(material_name)

	var custom_materials := _find_custom_materials(node)
	if custom_materials.size() > 0:
		option_button.add_separator("Custom Materials")
		for material_name in custom_materials:
			option_button.add_item(material_name)

	var current_material := ""
	if node.has_meta(MATERIAL_META_KEY):
		current_material = str(node.get_meta(MATERIAL_META_KEY))

	option_button.selected = 0
	if current_material != "":
		for i in range(option_button.item_count):
			if option_button.get_item_text(i).to_lower() == current_material.to_lower():
				option_button.selected = i
				break

	option_button.item_selected.connect(_on_material_selected.bind(node, option_button))
	row.add_child(option_button)

	return row

func _make_use_flat_transmission_row(node: Node) -> HBoxContainer:
	var row := HBoxContainer.new()

	var label := Label.new()
	label.text = "Use Flat Transmission"
	label.custom_minimum_size.x = 120 * EditorInterface.get_editor_scale()
	row.add_child(label)

	var check_box := CheckBox.new()

	var use_flat_transmission := false
	if node.has_meta(USE_FLAT_TRANSMISSION_META_KEY):
		use_flat_transmission = node.get_meta(USE_FLAT_TRANSMISSION_META_KEY)

	check_box.button_pressed = use_flat_transmission
	check_box.toggled.connect(_on_use_flat_transmission_toggled.bind(node))
	row.add_child(check_box)

	return row

func _on_material_selected(index: int, node: Node, option_button: OptionButton):
	if index == 0:
		node.remove_meta(MATERIAL_META_KEY)
	else:
		node.set_meta(MATERIAL_META_KEY, option_button.get_item_text(index).to_lower())

	EditorInterface.mark_scene_as_unsaved()
	_sync_running_game(node)

func _on_use_flat_transmission_toggled(toggled_on: bool, node: Node):
	if toggled_on:
		node.set_meta(USE_FLAT_TRANSMISSION_META_KEY, true)
	else:
		node.remove_meta(USE_FLAT_TRANSMISSION_META_KEY)

	EditorInterface.mark_scene_as_unsaved()
	_sync_running_game(node)

func set_debugger_plugin(plugin) -> void:
	debugger_plugin = plugin

# Custom EditorInspectorPlugin controls only ever run against the editor's local copy of the
# scene - there's no way to reach a Node in the running game's separate process directly, so this
# instead sends the node's path (relative to the edited scene root, which the running game's scene
# root mirrors) over the debugger protocol - see VADebuggerPlugin. The running game's copy of this
# node was never touched by the set_meta/remove_meta call that just ran above on the editor's local
# copy, so the current metadata values have to be sent too, for the receiving end to apply to its
# own copy before re-adding the primitive.
func _sync_running_game(node: Node) -> void:
	if debugger_plugin == null:
		return

	var scene_root := EditorInterface.get_edited_scene_root()
	if scene_root == null:
		return

	var material := ""
	if node.has_meta(MATERIAL_META_KEY):
		material = str(node.get_meta(MATERIAL_META_KEY))

	var use_flat_transmission = null
	if node.has_meta(USE_FLAT_TRANSMISSION_META_KEY):
		use_flat_transmission = node.get_meta(USE_FLAT_TRANSMISSION_META_KEY)

	var node_path := scene_root.get_path_to(node)
	debugger_plugin.sync_primitive(scene_root.name, node_path, material, use_flat_transmission)

# VACustomMaterial nodes only register themselves in customMaterials at runtime
# (VACustomMaterial._EnterTree bails out early when Engine.IsEditorHint() is true), so the edited
# scene tree has to be walked directly to find their names while in the editor.
func _find_custom_materials(node: Node) -> Array:
	var scene_root := EditorInterface.get_edited_scene_root()
	if scene_root == null:
		return []

	var names := []
	_find_custom_materials_recursive(scene_root, names)
	return names

func _find_custom_materials_recursive(node: Node, names: Array):
	if node is VACustomMaterial:
		names.append(node.MaterialName)

	for child in node.get_children():
		_find_custom_materials_recursive(child, names)
