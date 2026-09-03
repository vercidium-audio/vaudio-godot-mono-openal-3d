@tool
extends EditorInspectorPlugin

# Adds a "Vercidium Audio" section to the Inspector for any spatial node (Node2D/Node3D), exposing the Material and Use Flat Transmission settings
var VAWorld: Script

func _init() -> void:
	VAWorld = load("%s/world/VAWorld.cs" % _addon_root())

# "res://addons/vaudio-godot-mono-openal-2d/common/editor/<this>.gd" -> ".../-2d"
func _addon_root() -> String:
	return get_script().resource_path.get_base_dir().get_base_dir().get_base_dir()

const MATERIAL_META_KEY = "vercidium_audio_material"
const USE_FLAT_TRANSMISSION_META_KEY = "vercidium_audio_use_flat_transmission"

# Controls which descendants a cascading material applies to. Only affects nodes that inherit
# the material from an ancestor - a node with its own material meta always keeps it.
const PROPAGATE_META_KEY = "vercidium_audio_propagate"
const PROPAGATE_LAYER_META_KEY = "vercidium_audio_propagate_layer"

# Index 0 is the default ("All") and is never written as metadata - it's the absence of the key.
const PROPAGATE_MODES = ["All", "Colliders only", "Meshes only"]
const PROPAGATE_MODE_META_VALUES = ["", "colliders", "visuals"]

var debugger_plugin

const BUILTIN_MATERIAL_NAMES = [
	"brick", "cloth", "concrete", "concretepolished", "dirt", "glass", "grass", "gravel",
	"gyprock", "ice", "leaf", "marble", "metal", "mud", "rock", "sand", "snow", "tile",
	"tree", "water", "woodindoor", "woodoutdoor",
]

func _can_handle(object):
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

	# Propagation controls only matter when this node has children to cascade into
	if node.get_child_count() > 0:
		section.add_child(_make_propagate_row(node))
		section.add_child(_make_propagate_layer_row(node))

	add_custom_control(section)

func _make_heading() -> Label:
	var label := Label.new()
	label.text = "Vercidium Audio"

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

func _make_propagate_row(node: Node) -> HBoxContainer:
	var row := HBoxContainer.new()

	var label := Label.new()
	label.text = "Propagate To"
	label.tooltip_text = "Which child nodes a material set on this node cascades down to.\n\nAll: every child (default)\nColliders only: only collision shape children - skips the visual mesh of a mesh + collider pair\nMeshes only: only mesh / non-collision children"
	label.custom_minimum_size.x = 120 * EditorInterface.get_editor_scale()
	row.add_child(label)

	var option_button := OptionButton.new()
	option_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL

	for mode_name in PROPAGATE_MODES:
		option_button.add_item(mode_name)

	option_button.selected = 0
	if node.has_meta(PROPAGATE_META_KEY):
		var current := str(node.get_meta(PROPAGATE_META_KEY)).to_lower()
		var found := PROPAGATE_MODE_META_VALUES.find(current)
		if found > 0:
			option_button.selected = found

	option_button.item_selected.connect(_on_propagate_selected.bind(node))
	row.add_child(option_button)

	return row

const PROPAGATE_LAYER_SELECTED_COLOR := Color(0.16, 0.47, 0.9)

func _make_propagate_layer_row(node: Node) -> HBoxContainer:
	var row := HBoxContainer.new()

	var label := Label.new()
	label.text = "Propagate Layers"
	label.tooltip_text = "Optional. When one or more layers are selected, the material only cascades to children that are on one of them.\n\nFor mesh children this is the visual render layer (3D only); for collider children it's the parent body's collision layer.\n\nSelect nothing to cascade regardless of layer."
	label.custom_minimum_size.x = 120 * EditorInterface.get_editor_scale()
	row.add_child(label)

	# A compact grid of 20 toggle buttons, mirroring Godot's built-in layer editor
	var grid := GridContainer.new()
	grid.columns = 10
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL

	var current_mask := 0
	if node.has_meta(PROPAGATE_LAYER_META_KEY):
		current_mask = int(node.get_meta(PROPAGATE_LAYER_META_KEY))

	var layer_buttons := []
	for i in range(20):
		var bit_button := Button.new()
		bit_button.toggle_mode = true
		bit_button.text = str(i + 1)
		bit_button.custom_minimum_size = Vector2(26, 26) * EditorInterface.get_editor_scale()
		bit_button.add_theme_font_size_override("font_size", roundi(10 * EditorInterface.get_editor_scale()))
		bit_button.add_theme_color_override("font_pressed_color", Color.WHITE)
		bit_button.add_theme_color_override("font_hover_pressed_color", Color.WHITE)
		bit_button.add_theme_stylebox_override("pressed", _selected_layer_stylebox())
		bit_button.add_theme_stylebox_override("hover_pressed", _selected_layer_stylebox())
		bit_button.tooltip_text = "Layer %d" % (i + 1)
		bit_button.button_pressed = (current_mask & (1 << i)) != 0
		grid.add_child(bit_button)
		layer_buttons.append(bit_button)

	for bit_button in layer_buttons:
		bit_button.toggled.connect(_on_propagate_layer_bit_toggled.bind(node, layer_buttons))

	row.add_child(grid)
	return row

func _selected_layer_stylebox() -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = PROPAGATE_LAYER_SELECTED_COLOR
	sb.set_corner_radius_all(roundi(3 * EditorInterface.get_editor_scale()))
	return sb

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

func _on_propagate_selected(index: int, node: Node):
	if index == 0:
		node.remove_meta(PROPAGATE_META_KEY)
	else:
		node.set_meta(PROPAGATE_META_KEY, PROPAGATE_MODE_META_VALUES[index])

	EditorInterface.mark_scene_as_unsaved()
	_sync_running_game(node)

func _on_propagate_layer_bit_toggled(_pressed: bool, node: Node, layer_buttons: Array):
	var mask := _mask_from_buttons(layer_buttons)

	# No layers selected means "don't restrict by layer" - store nothing
	if mask == 0:
		node.remove_meta(PROPAGATE_LAYER_META_KEY)
	else:
		node.set_meta(PROPAGATE_LAYER_META_KEY, mask)

	EditorInterface.mark_scene_as_unsaved()
	_sync_running_game(node)

func _mask_from_buttons(layer_buttons: Array) -> int:
	var mask := 0
	for i in range(layer_buttons.size()):
		if layer_buttons[i].button_pressed:
			mask |= (1 << i)
	return mask

func set_debugger_plugin(plugin) -> void:
	debugger_plugin = plugin

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

	var propagate := ""
	if node.has_meta(PROPAGATE_META_KEY):
		propagate = str(node.get_meta(PROPAGATE_META_KEY))

	var propagate_layer = null
	if node.has_meta(PROPAGATE_LAYER_META_KEY):
		propagate_layer = int(node.get_meta(PROPAGATE_LAYER_META_KEY))

	var node_path := scene_root.get_path_to(node)
	debugger_plugin.sync_primitive(scene_root.name, node_path, material, use_flat_transmission, propagate, propagate_layer)

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
