@tool
extends EditorInspectorPlugin

var VAWorld: Script

func _init() -> void:
	VAWorld = load("%s/world/VAWorld.cs" % _addon_root())

# "res://addons/vaudio-godot-mono-openal-2d/common/editor/<this>.gd" -> ".../-2d"
func _addon_root() -> String:
	return get_script().resource_path.get_base_dir().get_base_dir().get_base_dir()

func _base_class_names() -> Dictionary:
	var spatial_al_source := "ALSource3D" if _addon_root().ends_with("-3d") else "ALSource2D"
	return {
		"VAListener": ["VAEmitter"],
		"VASource": ["VARaytracedSource", spatial_al_source, "ALSource"],
		"VASourceLeech": [spatial_al_source, "ALSource"],
		"VASourceRelative": ["ALSourceRelative", "ALSource"],
		"VASourceAmbient": ["ALSourceRelative", "ALSource"],
	}

# class_name -> (property_name -> tooltip text). Keyed by the C# class name as it appears in the
# XML doc's member id (e.g. "VAEmitter"), not by script file path.
var _tooltips := {}
var _xml_mtime := -1

# Tooltips queued during this inspector pass, applied once in _parse_end.
var _pending_tooltips := {}

func _can_handle(object) -> bool:
	# is_instance_of for VAWorld because it's a runtime-loaded Script var, not a parse-time type
	# (see _init) - `object is VAWorld` is a parse error for that form.
	return is_instance_of(object, VAWorld) or object is VAEmitter or object is VASource \
		or object is VASourceRelative or object is VASourceAmbient or object is VASourceLeech \
		or object is VAVisualisation \
		or object is VADefaultMaterial or object is VACustomMaterial

func _parse_begin(object) -> void:
	_reload_if_changed()
	_pending_tooltips.clear()

func _parse_property(object: Object, type: Variant.Type, name: String, hint_type: PropertyHint,
		hint_string: String, usage_flags: int, wide: bool) -> bool:

	for class_name_key in _get_class_name_chain(object):
		if not _tooltips.has(class_name_key):
			continue

		var class_tooltips: Dictionary = _tooltips[class_name_key]
		if class_tooltips.has(name):
			_pending_tooltips[name] = class_tooltips[name]
			break

	return false

func _parse_end(object) -> void:
	if _pending_tooltips.is_empty():
		return

	var tooltips := _pending_tooltips.duplicate()
	call_deferred("_apply_tooltips_deferred", tooltips)

func _apply_tooltips_deferred(tooltips: Dictionary) -> void:
	call_deferred("_apply_tooltips", tooltips)

func _apply_tooltips(tooltips: Dictionary) -> void:
	var inspector := EditorInterface.get_inspector()
	if inspector == null:
		return

	_apply_tooltips_recursive(inspector, tooltips)

func _apply_tooltips_recursive(node: Node, tooltips: Dictionary) -> void:
	if node is EditorProperty:
		var property_name: String = node.get_edited_property()
		if tooltips.has(property_name):
			_set_tooltip_recursive(node, tooltips[property_name])

	for child in node.get_children():
		_apply_tooltips_recursive(child, tooltips)

func _set_tooltip_recursive(node: Node, text: String) -> void:
	if node is Control:
		node.tooltip_text = text

	for child in node.get_children():
		_set_tooltip_recursive(child, text)

func _get_class_name_chain(object: Object) -> Array:
	var names := []
	var script := object.get_script()

	if script != null:
		var script_path: String = script.resource_path
		if script_path.ends_with(".cs"):
			var own_name := script_path.get_file().get_basename()
			names.append(own_name)

			var base_names = _base_class_names().get(own_name)
			if base_names != null:
				names.append_array(base_names)

	return names

func _reload_if_changed() -> void:
	var project_name: String = ProjectSettings.get_setting("application/config/name")
	var xml_path := "res://.godot/mono/temp/bin/Debug/%s.xml" % project_name

	if not FileAccess.file_exists(xml_path):
		return

	var mtime := FileAccess.get_modified_time(xml_path)
	if mtime == _xml_mtime:
		return

	_xml_mtime = mtime
	_tooltips = _parse_doc_xml(xml_path)

func _parse_doc_xml(xml_path: String) -> Dictionary:
	var result := {}

	var xml := XMLParser.new()
	if xml.open(xml_path) != OK:
		return result

	var in_summary := false
	var current_class := ""
	var current_property := ""
	var summary_text := ""

	while xml.read() == OK:
		var node_type := xml.get_node_type()

		if node_type == XMLParser.NODE_ELEMENT and xml.get_node_name() == "member":
			var member_id: String = xml.get_named_attribute_value_safe("name")
			var split := _split_member_id(member_id)
			current_class = split[0]
			current_property = split[1]
			in_summary = false

		elif node_type == XMLParser.NODE_ELEMENT and xml.get_node_name() == "summary":
			in_summary = true
			summary_text = ""

		elif node_type == XMLParser.NODE_TEXT and in_summary:
			summary_text += xml.get_node_data()

		elif node_type == XMLParser.NODE_ELEMENT and xml.get_node_name() == "see" and in_summary:
			summary_text += _cref_display_name(xml.get_named_attribute_value_safe("cref"))

		elif node_type == XMLParser.NODE_ELEMENT_END and xml.get_node_name() == "summary":
			in_summary = false

			if current_class != "" and current_property != "":
				var normalized := _normalize_summary(summary_text)
				if normalized != "":
					if not result.has(current_class):
						result[current_class] = {}
					result[current_class][current_property] = normalized

	return result

func _split_member_id(member_id: String) -> Array:
	if not (member_id.begins_with("P:") or member_id.begins_with("F:")):
		return ["", ""]

	var without_prefix := member_id.substr(2)
	var last_dot := without_prefix.rfind(".")
	if last_dot <= 0:
		return ["", ""]

	var property_name := without_prefix.substr(last_dot + 1)
	var type_name := without_prefix.substr(0, last_dot)
	var namespace_dot := type_name.rfind(".")
	var class_name_only := type_name.substr(namespace_dot + 1) if namespace_dot >= 0 else type_name

	return [class_name_only, property_name]

func _cref_display_name(cref: String) -> String:
	var colon := cref.find(":")
	var without_prefix := cref.substr(colon + 1) if colon >= 0 else cref

	var last_dot := without_prefix.rfind(".")
	return without_prefix.substr(last_dot + 1) if last_dot >= 0 else without_prefix

func _normalize_summary(raw: String) -> String:
	var lines := raw.replace("\r\n", "\n").split("\n")
	var normalized_lines := []

	for line in lines:
		var collapsed := " ".join(line.split(" ", false))
		normalized_lines.append(collapsed.strip_edges())

	while normalized_lines.size() > 0 and normalized_lines[0] == "":
		normalized_lines.remove_at(0)
	while normalized_lines.size() > 0 and normalized_lines[-1] == "":
		normalized_lines.remove_at(normalized_lines.size() - 1)

	return "\n".join(normalized_lines)
