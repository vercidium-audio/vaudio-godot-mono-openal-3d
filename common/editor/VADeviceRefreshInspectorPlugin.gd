@tool
extends EditorInspectorPlugin

func _can_handle(object) -> bool:
	return object is VAInputStreamSource

func _parse_property(object: Object, type: Variant.Type, name: String, hint_type: PropertyHint,
		hint_string: String, usage_flags: int, wide: bool) -> bool:
	if not (object is VAInputStreamSource and name == "BufferSizeFrames"):
		return false

	var refresh_button := Button.new()
	refresh_button.text = "Refresh Audio Devices"
	refresh_button.pressed.connect(object.RefreshDevices)
	add_custom_control(refresh_button)

	return false
