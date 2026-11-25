@tool
extends RefCounted

# Registers the audio/vaudio/* Project Settings for the 2D and 3D plugins. Settings that only apply to one audio backend start with its prefix, and are hidden while another backend is selected

const BACKEND_SETTING = "audio/vaudio/backend"
const DEFAULT_BACKEND = "OpenAL"
const BACKEND_PREFIXES = {
	"OpenAL": "audio/vaudio/openal_",
	"FMOD": "audio/vaudio/fmod_",
}

const DEFAULT_DEVICE_LABEL = "System Default"
const DEFAULT_HRTF_LABEL = "Default"

# The backend the visible settings were last picked for
static var _visible_backend := ""

static func register() -> void:
	# Each backend's library is loaded once at startup, and mono needs a rebuild to switch
	_add("audio/vaudio/backend", DEFAULT_BACKEND, TYPE_STRING, PROPERTY_HINT_ENUM, ",".join(BACKEND_PREFIXES.keys()))
	ProjectSettings.set_restart_if_changed(BACKEND_SETTING, true)

	# The dropdown is filled by VAWorld.RefreshOutputDeviceList()
	_add("audio/vaudio/output_device", DEFAULT_DEVICE_LABEL, TYPE_STRING, PROPERTY_HINT_ENUM, DEFAULT_DEVICE_LABEL)

	# 0 means the driver default
	_add("audio/vaudio/sample_rate", 0, TYPE_INT, PROPERTY_HINT_ENUM, "System Default:0,22050,44100,48000,96000")

	# Dev-only setting (not end-user-facing)
	_add("audio/vaudio/openal_max_reverb_sends", 1, TYPE_INT, PROPERTY_HINT_RANGE, "1,16,or_greater")

	_add("audio/vaudio/openal_hrtf_enabled", true, TYPE_BOOL)

	# Same DEFAULT_*_LABEL storage as output_device. The dropdown is filled by VAWorld.RefreshOutputDeviceList()
	_add("audio/vaudio/openal_hrtf", DEFAULT_HRTF_LABEL, TYPE_STRING, PROPERTY_HINT_ENUM, DEFAULT_HRTF_LABEL)

	# .mhr files in here are listed alongside OpenAL Soft's built-in HRTF, and are bundled into exports by VAHRTFExportPlugin
	_add("audio/vaudio/openal_hrtf_folder", "res://hrtf", TYPE_STRING, PROPERTY_HINT_DIR)

	# Read once at device-open time, can't be changed at runtime
	_add("audio/vaudio/openal_max_mono_sources", 16, TYPE_INT, PROPERTY_HINT_RANGE, "0,256,or_greater")
	_add("audio/vaudio/openal_max_stereo_sources", 240, TYPE_INT, PROPERTY_HINT_RANGE, "0,256,or_greater")

	update_backend_visibility()

static func _add(name: String, default_value: Variant, type: int, hint: int = PROPERTY_HINT_NONE, hint_string: String = "") -> void:
	if not ProjectSettings.has_setting(name):
		ProjectSettings.set_setting(name, default_value)

	ProjectSettings.set_initial_value(name, default_value)

	ProjectSettings.add_property_info({
		"name": name,
		"type": type,
		"hint": hint,
		"hint_string": hint_string,
	})

# Hides the other backends' settings. Internal settings keep their saved values in project.godot, so switching back shows them unchanged. Connect to ProjectSettings.settings_changed
static func update_backend_visibility() -> void:
	var selected: String = ProjectSettings.get_setting(BACKEND_SETTING, DEFAULT_BACKEND)

	if selected == _visible_backend:
		return

	_visible_backend = selected

	for property in ProjectSettings.get_property_list():
		for backend in BACKEND_PREFIXES:
			if property.name.begins_with(BACKEND_PREFIXES[backend]):
				ProjectSettings.set_as_internal(property.name, backend != selected)
