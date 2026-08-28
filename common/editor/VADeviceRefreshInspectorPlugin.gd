@tool
extends EditorInspectorPlugin

# Adds a "Refresh OpenAL Devices" button right below DeviceName under VAInputStreamSource in the
# Inspector - that property's dropdown (the node's own _ValidateProperty) is only re-queried when
# Godot rebuilds the node's cached property list, which doesn't happen on its own when a device is
# plugged in/unplugged while the node stays selected. The button calls the node's own
# RefreshDevices(), which re-queries the driver and forces that rebuild via
# NotifyPropertyListChanged. Placed via _parse_property (called once per property, in list order)
# instead of _parse_end so it lands inline in that node's own section instead of at the very
# bottom of the Inspector, after every other section - see below for why this matches on the NEXT
# property after the device dropdown rather than the dropdown property itself. Mirrors the native
# plugin's VADeviceRefreshInspectorPlugin (va_conversion_plugin.cpp).
#
# Shared verbatim between the 2D and 3D addons via each addon's `common` symlink - VAInputStreamSource
# is [GlobalClass] so it resolves by name here without an addon-specific preload().

func _can_handle(object) -> bool:
	return object is VAInputStreamSource

# add_custom_control() (unlike add_property_editor's add_to_end flag) inserts the control just
# before the current property's own row is drawn, not after it - so to land the button visually
# below DeviceName, this has to match on the NEXT property in VAInputStreamSource's exported
# property order (BufferSizeFrames after DeviceName) rather than the device property itself.
func _parse_property(object: Object, type: Variant.Type, name: String, hint_type: PropertyHint,
		hint_string: String, usage_flags: int, wide: bool) -> bool:
	if not (object is VAInputStreamSource and name == "BufferSizeFrames"):
		return false

	var refresh_button := Button.new()
	refresh_button.text = "Refresh OpenAL Devices"
	refresh_button.pressed.connect(object.RefreshDevices)
	add_custom_control(refresh_button)

	return false
