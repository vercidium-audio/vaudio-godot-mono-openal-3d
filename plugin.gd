# AI-created

@tool
extends EditorPlugin

var main_plugin: EditorPlugin

const ADDON_NAME = "vaudio-godot-mono-openal-3d"
const ADDON_ROOT = "res://addons/" + ADDON_NAME
const LOG_PREFIX = "[" + ADDON_NAME + "] "
const VACsproj = preload("res://addons/vaudio-godot-mono-openal-3d/common/editor/VACsproj.gd")
const VAProjectSettings = preload("res://addons/vaudio-godot-mono-openal-3d/common/editor/VAProjectSettings.gd")

var _setup_done := false
var _no_csproj_error_shown := false
var _needs_rebuild_error_shown := false
var _wrong_godot_version_error_shown := false
var _backend := ""

func _enter_tree():
	if not ProjectSettings.settings_changed.is_connected(_on_settings_changed):
		ProjectSettings.settings_changed.connect(_on_settings_changed)

	_backend = VAProjectSettings.selected_backend()
	_setup_project()

func _exit_tree():
	if ProjectSettings.settings_changed.is_connected(_on_settings_changed):
		ProjectSettings.settings_changed.disconnect(_on_settings_changed)

	if main_plugin:
		main_plugin._exit_tree()
		main_plugin.queue_free()
		main_plugin = null

func _on_settings_changed():
	# The .csproj compiles only the selected backend (see VACsproj.gd), so the C# assembly has to be rebuilt
	var backend := VAProjectSettings.selected_backend()
	if backend != _backend:
		_backend = backend
		push_warning(LOG_PREFIX + "The audio backend is now %s. Rebuild the C# project (Alt + B, or the hammer icon in the top right) and restart the editor to switch to it" % backend)

	if not _setup_done:
		_setup_project()

func _get_csproj_path() -> String:
	var project_name = ProjectSettings.get_setting("application/config/name")
	return "res://%s.csproj" % project_name

func _setup_project():
	if not ClassDB.class_exists("CSharpScript"):
		if not _wrong_godot_version_error_shown:
			_wrong_godot_version_error_shown = true
			push_error(LOG_PREFIX + "This plugin requires the .NET (C#) version of Godot, but you're running the standard version. Please download Godot .NET from https://godotengine.org/download and disable this plugin in the meantime")
		return

	var csproj_path = _get_csproj_path()

	if not FileAccess.file_exists(csproj_path):
		if not _no_csproj_error_shown:
			_no_csproj_error_shown = true
			push_error(LOG_PREFIX + "No C# solution found. This plugin requires C# - please create a C# solution (Project → Tools → C# → Create C# Solution) and then re-enable this plugin")
		return

	var file = FileAccess.open(csproj_path, FileAccess.READ)
	if not file:
		return

	var content = file.get_as_text()
	file.close()

	var dll_res_path = ADDON_ROOT + "/bin/vaudio.dll"
	var dll_exists = FileAccess.file_exists(dll_res_path)

	if "</Project>" not in content:
		push_error(LOG_PREFIX + "Could not find a </Project> tag in the .csproj file")
		return

	var new_content := VACsproj.update(content, ADDON_NAME)

	if new_content != content:
		file = FileAccess.open(csproj_path, FileAccess.WRITE)
		if file:
			file.store_string(new_content)
			file.close()
			print(LOG_PREFIX + "Added vaudio references to ", ProjectSettings.globalize_path(csproj_path))

		push_error(LOG_PREFIX + "Updated your .csproj with the vaudio references. Godot needs to rebuild the C# assembly before the plugin can fully load - please rebuild the project (Alt + B, or hammer icon in the top right) and then disable and re-enable this plugin")
		return

	if dll_exists:
		print(LOG_PREFIX + "vaudio.dll found")
	else:
		push_error(LOG_PREFIX + "vaudio.dll not found - please copy your vaudio.dll into %s, then disable and enable the Vercidium Audio plugin" % ProjectSettings.globalize_path(dll_res_path.get_base_dir()))

	var probe_script = load(ADDON_ROOT + "/common/nodes/VAEmitter.cs")
	if probe_script == null or probe_script.get_instance_base_type() == "":
		if not _needs_rebuild_error_shown:
			_needs_rebuild_error_shown = true
			push_error(LOG_PREFIX + "Godot needs to rebuild the C# assembly before the plugin can fully load - please rebuild the project (Alt + B, or hammer icon in the top right) and then disable and re-enable this plugin")
		return

	_setup_done = true

	if main_plugin:
		return

	var main_script = load(ADDON_ROOT + "/plugin_main.gd")
	main_plugin = main_script.new()
	main_plugin._enter_tree()
