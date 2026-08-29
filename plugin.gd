@tool
extends EditorPlugin

var main_plugin: EditorPlugin

const VAUDIO_DLL_HINT_PATH = "addons\\vaudio-godot-mono-openal-3d\\bin\\vaudio.dll"

const CSPROJ_INSERT = """    <ItemGroup>
        <Reference Include="vaudio">
            <HintPath>%s</HintPath>
        </Reference>
    </ItemGroup>""" % VAUDIO_DLL_HINT_PATH

const PACKAGE_REFERENCES = """    <ItemGroup>
        <PackageReference Include="openal_soft_bindings" Version="1.0.10" />
    </ItemGroup>"""

const PROPERTY_GROUP = """    <PropertyGroup>
        <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
        <NoWarn>$(NoWarn);1591</NoWarn>
    </PropertyGroup>"""

const DLL_SOURCE_WINDOWS = "addons/vaudio-godot-mono-openal-3d/bin/soft_oal.dll"
const DLL_SOURCE_LINUX = "addons/vaudio-godot-mono-openal-3d/bin/libopenal.so.1"

var _setup_done := false
var _no_csproj_error_shown := false
var _needs_rebuild_error_shown := false
var _wrong_godot_version_error_shown := false

func _enter_tree():
	if not ProjectSettings.settings_changed.is_connected(_on_settings_changed):
		ProjectSettings.settings_changed.connect(_on_settings_changed)

	_setup_project()

func _exit_tree():
	if ProjectSettings.settings_changed.is_connected(_on_settings_changed):
		ProjectSettings.settings_changed.disconnect(_on_settings_changed)

	if main_plugin:
		main_plugin._exit_tree()
		main_plugin.queue_free()
		main_plugin = null

func _on_settings_changed():
	if not _setup_done:
		_setup_project()

func _get_csproj_path() -> String:
	var project_name = ProjectSettings.get_setting("application/config/name")
	return "res://%s.csproj" % project_name

func _setup_project():
	if not ClassDB.class_exists("CSharpScript"):
		if not _wrong_godot_version_error_shown:
			_wrong_godot_version_error_shown = true
			push_error("[vaudio-godot-mono-openal-3d] This plugin requires the .NET (C#) version of Godot, but you're running the standard version. Please download Godot .NET from https://godotengine.org/download and disable this plugin in the meantime")
		return

	var csproj_path = _get_csproj_path()

	if not FileAccess.file_exists(csproj_path):
		if not _no_csproj_error_shown:
			_no_csproj_error_shown = true
			push_error("[vaudio-godot-mono-openal-3d] No C# solution found. This plugin requires C# - please create a C# solution (Project → Tools → C# → Create C# Solution) and then re-enable this plugin")
		return

	var file = FileAccess.open(csproj_path, FileAccess.READ)
	if not file:
		return

	var content = file.get_as_text()
	file.close()

	var dll_res_path = "res://addons/vaudio-godot-mono-openal-3d/bin/vaudio.dll"
	var dll_exists = FileAccess.file_exists(dll_res_path)

	var insert_content = ""
	if "vaudio.dll" not in content:
		insert_content += "\n" + CSPROJ_INSERT + "\n"

	if "openal_soft_bindings" not in content:
		insert_content += "\n" + PROPERTY_GROUP + "\n\n" + PACKAGE_REFERENCES + "\n"

	if insert_content != "":
		var insert_pos = content.rfind("</Project>")
		if insert_pos == -1:
			push_error("[vaudio-godot-mono-openal-3d] Could not find a </Project> tag in the .csproj file")
			return

		var new_content = content.substr(0, insert_pos) + insert_content + content.substr(insert_pos)

		file = FileAccess.open(csproj_path, FileAccess.WRITE)
		if file:
			file.store_string(new_content)
			file.close()
			print("[vaudio-godot-mono-openal-3d] Added vaudio references to ", ProjectSettings.globalize_path(csproj_path))

		push_error("[vaudio-godot-mono-openal-3d] Updated your .csproj with the vaudio references. Godot needs to rebuild the C# assembly before the plugin can fully load - please rebuild the project (Alt + B, or hammer icon in the top right) and then disable and re-enable this plugin")
		return

	if dll_exists:
		print("[vaudio-godot-mono-openal-3d] vaudio.dll found")
	else:
		push_error("[vaudio-godot-mono-openal-3d] vaudio.dll not found - please copy your vaudio.dll into %s, then disable and enable the Vercidium Audio plugin" % ProjectSettings.globalize_path(dll_res_path.get_base_dir()))

	var probe_script = load("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VAEmitter.cs")
	if probe_script == null or probe_script.get_instance_base_type() == "":
		if not _needs_rebuild_error_shown:
			_needs_rebuild_error_shown = true
			push_error("[vaudio-godot-mono-openal-3d] Godot needs to rebuild the C# assembly before the plugin can fully load - please rebuild the project (Alt + B, or hammer icon in the top right) and then disable and re-enable this plugin")
		return

	_copy_dll()

	_setup_done = true

	if main_plugin:
		return

	var main_script = load("res://addons/vaudio-godot-mono-openal-3d/plugin_main.gd")
	main_plugin = main_script.new()
	main_plugin._enter_tree()

func _copy_dll():
	var source_path: String
	var dest_path: String
	var lib_name: String

	if OS.get_name() == "Windows":
		source_path = DLL_SOURCE_WINDOWS
		dest_path = "res://soft_oal.dll"
		lib_name = "soft_oal.dll"
	elif OS.get_name() == "Linux":
		source_path = DLL_SOURCE_LINUX
		dest_path = "res://libopenal.so.1"
		lib_name = "libopenal.so.1"
	else:
		return

	# Check if library already exists at destination
	if FileAccess.file_exists(dest_path):
		return

	# Copy the library
	if FileAccess.file_exists(source_path):
		var result = DirAccess.copy_absolute(source_path, dest_path)
		if result == OK:
			print("[vaudio-godot-mono-openal-3d] Copied %s to project root" % lib_name)
		else:
			push_error("[vaudio-godot-mono-openal-3d] Failed to copy %s: %s" % [lib_name, result])
	else:
		push_error("[vaudio-godot-mono-openal-3d] Source library not found at ", source_path)
