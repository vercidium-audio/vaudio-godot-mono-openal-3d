@tool
extends EditorInspectorPlugin

# Adds a "Vercidium Audio" section to the Inspector for any Node3D, exposing the Material and
# Supports Permeation settings as an OptionButton/CheckBox rather than requiring the user to edit
# the vercidium_audio_material / vercidium_audio_supports_permeation metadata fields by hand.
# Both settings are inherited down the scene tree at runtime (see VAWorldPrimitives.AddPrimitive),
# so setting either on a plain Node3D configures its whole subtree at once.

const VAWorld = preload("res://addons/vaudio-godot-openal/main/VAWorld.cs")
const VAEmitter = preload("res://addons/vaudio-godot-openal/emitter/VAEmitter.cs")
const VAMaterial = preload("res://addons/vaudio-godot-openal/nodes/VAMaterial.cs")
const VASource = preload("res://addons/vaudio-godot-openal/source/VASource.cs")
const VASourceRelative = preload("res://addons/vaudio-godot-openal/nodes/VASourceRelative.cs")
const VASourceAmbient = preload("res://addons/vaudio-godot-openal/nodes/VASourceAmbient.cs")
const VASourceLeech = preload("res://addons/vaudio-godot-openal/nodes/VASourceLeech.cs")

const MATERIAL_META_KEY = "vercidium_audio_material"
const SUPPORTS_PERMEATION_META_KEY = "vercidium_audio_supports_permeation"

const BUILTIN_MATERIAL_NAMES = [
	"brick", "cloth", "concrete", "concretepolished", "dirt", "glass", "grass", "gravel",
	"gyprock", "ice", "leaf", "marble", "metal", "mud", "rock", "sand", "snow", "tile",
	"tree", "water", "woodindoor", "woodoutdoor",
]

func _can_handle(object):
	if not (object is Node3D):
		return false

	# Audio control/geometry-source nodes, not raytraced geometry - excluded the same way the
	# native VAMaterialInspectorPlugin excludes them.
	return not (object is VAWorld or object is VAEmitter or object is VAMaterial
		or object is VASource or object is VASourceRelative or object is VASourceAmbient
		or object is VASourceLeech)

func _parse_end(object):
	var node := object as Node3D
	if node == null:
		return

	var section := VBoxContainer.new()
	section.add_child(_make_heading())

	section.add_child(_make_material_row(node))
	section.add_child(_make_supports_permeation_row(node))

	add_custom_control(section)

func _make_heading() -> Label:
	var label := Label.new()
	label.text = "Vercidium Audio"

	# Raw pixel font-size overrides aren't multiplied by the editor's UI scale (unlike the
	# built-in theme), so on high OS/editor scale factors this rendered tiny - scale it manually.
	label.add_theme_font_size_override("font_size", roundi(14 * EditorInterface.get_editor_scale()))
	return label

func _make_material_row(node: Node3D) -> HBoxContainer:
	var row := HBoxContainer.new()

	var label := Label.new()
	label.text = "Material"
	label.custom_minimum_size.x = 120 * EditorInterface.get_editor_scale()
	row.add_child(label)

	var option_button := OptionButton.new()
	option_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL

	# Index 0 means "no material" - not stored as metadata, removes it instead.
	option_button.add_item("Air (no geometry)")

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

func _make_supports_permeation_row(node: Node3D) -> HBoxContainer:
	var row := HBoxContainer.new()

	var label := Label.new()
	label.text = "Supports Permeation"
	label.custom_minimum_size.x = 120 * EditorInterface.get_editor_scale()
	row.add_child(label)

	var check_box := CheckBox.new()

	var supports_permeation := true
	if node.has_meta(SUPPORTS_PERMEATION_META_KEY):
		supports_permeation = node.get_meta(SUPPORTS_PERMEATION_META_KEY)

	check_box.button_pressed = supports_permeation
	check_box.toggled.connect(_on_supports_permeation_toggled.bind(node))
	row.add_child(check_box)

	return row

func _on_material_selected(index: int, node: Node3D, option_button: OptionButton):
	if index == 0:
		node.remove_meta(MATERIAL_META_KEY)
	else:
		node.set_meta(MATERIAL_META_KEY, option_button.get_item_text(index))

	EditorInterface.mark_scene_as_unsaved()

func _on_supports_permeation_toggled(toggled_on: bool, node: Node3D):
	if toggled_on:
		node.remove_meta(SUPPORTS_PERMEATION_META_KEY)
	else:
		node.set_meta(SUPPORTS_PERMEATION_META_KEY, false)

	EditorInterface.mark_scene_as_unsaved()

# VAMaterial nodes only register themselves in customMaterials at runtime (VAMaterial._EnterTree
# bails out early when Engine.IsEditorHint() is true), so the edited scene tree has to be walked
# directly to find their names while in the editor.
func _find_custom_materials(node: Node3D) -> Array:
	var scene_root := EditorInterface.get_edited_scene_root()
	if scene_root == null:
		return []

	var names := []
	_find_custom_materials_recursive(scene_root, names)
	return names

func _find_custom_materials_recursive(node: Node, names: Array):
	if node is VAMaterial:
		names.append(node.MaterialName)

	for child in node.get_children():
		_find_custom_materials_recursive(child, names)
