@tool
extends EditorPlugin

const VAWorld = preload("res://addons/vaudio-godot-openal/main/VAWorld.cs")
const VAEmitter = preload("res://addons/vaudio-godot-openal/emitter/VAEmitter.cs")
const VAMaterial = preload("res://addons/vaudio-godot-openal/nodes/VAMaterial.cs")

const VASource = preload("res://addons/vaudio-godot-openal/source/VASource.cs")
const VASourceRelative = preload("res://addons/vaudio-godot-openal/nodes/VASourceRelative.cs")
const VASourceAmbient = preload("res://addons/vaudio-godot-openal/nodes/VASourceAmbient.cs")
const VASourceLeech = preload("res://addons/vaudio-godot-openal/nodes/VASourceLeech.cs")

const VAWorldGizmoPlugin = preload("res://addons/vaudio-godot-openal/editor/VAWorldGizmoPlugin.gd")
const VAMaterialInspectorPlugin = preload("res://addons/vaudio-godot-openal/editor/VAMaterialInspectorPlugin.gd")
const VAConversionContextMenuPlugin = preload("res://addons/vaudio-godot-openal/editor/VAConversionContextMenuPlugin.gd")
const VADebuggerPlugin = preload("res://addons/vaudio-godot-openal/editor/VADebuggerPlugin.gd")

var world_gizmo_plugin
var material_inspector_plugin
var conversion_context_menu_plugin
var debugger_plugin

const VAUDIO_DLL_HINT_PATH = "addons\\vaudio-godot-openal\\bin\\vaudio.dll"

const CSPROJ_INSERT = """    <ItemGroup>
        <Reference Include="vaudio">
            <HintPath>%s</HintPath>
        </Reference>
    </ItemGroup>""" % VAUDIO_DLL_HINT_PATH

func _enter_tree():
	var icon = preload("res://addons/vaudio-godot-openal/icons/vercidium.svg")
	var iconAL = preload("res://addons/vaudio-godot-openal/icons/vercidium_al.svg")

	add_custom_type("VAWorld", "Node3D", VAWorld, icon)
	add_custom_type("VAEmitter", "Node3D", VAEmitter, icon)
	add_custom_type("VAMaterial", "Node3D", VAMaterial, icon)

	add_custom_type("VASource", "Node3D", VASource, iconAL)
	add_custom_type("VASourceRelative", "Node", VASourceRelative, iconAL)
	add_custom_type("VASourceAmbient", "Node", VASourceAmbient, iconAL)
	add_custom_type("VASourceLeech", "Node3D", VASourceLeech, iconAL)

	world_gizmo_plugin = VAWorldGizmoPlugin.new()
	add_node_3d_gizmo_plugin(world_gizmo_plugin)

	debugger_plugin = VADebuggerPlugin.new()
	add_debugger_plugin(debugger_plugin)

	material_inspector_plugin = VAMaterialInspectorPlugin.new()
	material_inspector_plugin.set_debugger_plugin(debugger_plugin)
	add_inspector_plugin(material_inspector_plugin)

	conversion_context_menu_plugin = VAConversionContextMenuPlugin.new()
	add_context_menu_plugin(EditorContextMenuPlugin.CONTEXT_SLOT_SCENE_TREE, conversion_context_menu_plugin)

	_setup_project()

	if not ProjectSettings.settings_changed.is_connected(_on_settings_changed):
		ProjectSettings.settings_changed.connect(_on_settings_changed)

	print("[vaudio-godot-openal] Vercidium Audio (vaudio) plugin enabled")

func _exit_tree():
	remove_custom_type("VAWorld")
	remove_custom_type("VAEmitter")
	remove_custom_type("VAMaterial")

	remove_custom_type("VASource")
	remove_custom_type("VASourceRelative")
	remove_custom_type("VASourceAmbient")
	remove_custom_type("VASourceLeech")

	if world_gizmo_plugin:
		remove_node_3d_gizmo_plugin(world_gizmo_plugin)
		world_gizmo_plugin = null

	if material_inspector_plugin:
		remove_inspector_plugin(material_inspector_plugin)
		material_inspector_plugin = null

	if debugger_plugin:
		remove_debugger_plugin(debugger_plugin)
		debugger_plugin = null

	if conversion_context_menu_plugin:
		remove_context_menu_plugin(conversion_context_menu_plugin)
		conversion_context_menu_plugin = null

	if ProjectSettings.settings_changed.is_connected(_on_settings_changed):
		ProjectSettings.settings_changed.disconnect(_on_settings_changed)

	print("Vercidium Audio (vaudio-godot-openal) plugin disabled")

var _setup_done := false

func _on_settings_changed():
	if not _setup_done:
		_setup_project()

func _setup_project():
	var project_name = ProjectSettings.get_setting("application/config/name")
	var csproj_path = "res://%s.csproj" % project_name

	if not FileAccess.file_exists(csproj_path):
		push_error("[vaudio-godot-openal] No C# solution found. This plugin requires C# - please create a C# solution (Project → Tools → C# → Create C# Solution) and then re-enable this plugin")
		return

	var file = FileAccess.open(csproj_path, FileAccess.READ)
	if not file:
		return

	var content = file.get_as_text()
	file.close()

	_setup_done = true

	var dll_res_path = "res://addons/vaudio-godot-openal/bin/vaudio.dll"
	var dll_exists = FileAccess.file_exists(dll_res_path)

	if "vaudio.dll" not in content:
		var insert_pos = content.rfind("</Project>")
		if insert_pos == -1:
			push_error("[vaudio-godot-openal] Could not find a </Project> tag in the .csproj file")
			return

		var new_content = content.substr(0, insert_pos) + "\n" + CSPROJ_INSERT + "\n" + content.substr(insert_pos)

		file = FileAccess.open(csproj_path, FileAccess.WRITE)
		if file:
			file.store_string(new_content)
			file.close()
			print("[vaudio-godot-openal] Added vaudio references to ", ProjectSettings.globalize_path(csproj_path))

	if dll_exists:
		print("[vaudio-godot-openal] csproj configured correctly")
	else:
		push_error("[vaudio-godot-openal] vaudio.dll not found - please copy your vaudio.dll into %s, then disable and enable the Vercidium Audio plugin" % ProjectSettings.globalize_path(dll_res_path.get_base_dir()))

func _enable_plugin():
	if not DirAccess.dir_exists_absolute("res://addons/godot-openal"):
		push_error("[vaudio-godot-openal] The 'godot-openal' plugin is required. Clone it from https://github.com/vercidium-audio/godot-openal and enable it first.")
		get_editor_interface().set_plugin_enabled("vaudio-godot-openal", false)