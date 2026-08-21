@tool
extends EditorPlugin

const VAEmitter = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VAEmitter.cs")
const VAListener = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VAListener.cs")
const ALSource = preload("res://addons/vaudio-godot-mono-openal-3d/openal/nodes/ALSource.cs")
const ALSource3D = preload("res://addons/vaudio-godot-mono-openal-3d/openal/nodes/ALSource3D.cs")
const VAStreamSource = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VAStreamSource.cs")
const VAInputStreamSource = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VAInputStreamSource.cs")
const VANetworkedStreamSource = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VANetworkedStreamSource.cs")
const VASource = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VASource.cs")
const VASourceLeech = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VASourceLeech.cs")

const ALSourceRelative = preload("res://addons/vaudio-godot-mono-openal-3d/openal/nodes/ALSourceRelative.cs")
const VASourceRelative = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VASourceRelative.cs")
const VASourceAmbient = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VASourceAmbient.cs")
const VAVisualisation = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VAVisualisation.cs")
const VAWorld = preload("res://addons/vaudio-godot-mono-openal-3d/world/VAWorld.cs")
const VADefaultMaterial = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VADefaultMaterial.cs")
const VACustomMaterial = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VACustomMaterial.cs")

const VAWorldGizmoPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/editor/VAWorldGizmoPlugin.gd")
const VAMaterialInspectorPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/editor/VAMaterialInspectorPlugin.gd")
const VAConversionContextMenuPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/editor/VAConversionContextMenuPlugin.gd")
const VADebuggerPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/editor/VADebuggerPlugin.gd")
const VADeviceRefreshInspectorPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/editor/VADeviceRefreshInspectorPlugin.gd")
const VAInspectorTooltipPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/editor/VAInspectorTooltipPlugin.gd")

var world_gizmo_plugin
var material_inspector_plugin
var conversion_context_menu_plugin
var debugger_plugin
var device_refresh_inspector_plugin
var inspector_tooltip_plugin

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

# "audio/vaudio/*" Project Settings
const DEFAULT_DEVICE_LABEL = "System Default"

func _enter_tree():
	var icon = preload("res://addons/vaudio-godot-mono-openal-3d/icons/vercidium.svg")
	var iconAL = preload("res://addons/vaudio-godot-mono-openal-3d/icons/vercidium_al.svg")

	add_custom_type("VAEmitter", "Node3D", VAEmitter, icon)
	add_custom_type("VAListener", "Node3D", VAListener, icon)

	add_custom_type("ALSource", "Node", ALSource, iconAL)
	add_custom_type("ALSource3D", "Node3D", ALSource3D, iconAL)
	add_custom_type("VAStreamSource", "Node3D", VAStreamSource, iconAL)
	add_custom_type("VAInputStreamSource", "Node3D", VAInputStreamSource, iconAL)
	add_custom_type("VANetworkedStreamSource", "Node3D", VANetworkedStreamSource, iconAL)
	add_custom_type("VASource", "Node3D", VASource, iconAL)
	add_custom_type("VASourceLeech", "Node3D", VASourceLeech, iconAL)

	add_custom_type("ALSourceRelative", "Node", ALSourceRelative, iconAL)
	add_custom_type("VASourceRelative", "Node", VASourceRelative, iconAL)
	add_custom_type("VASourceAmbient", "Node", VASourceAmbient, iconAL)

	add_custom_type("VAVisualisation", "Node3D", VAVisualisation, icon)
	add_custom_type("VAWorld", "Node3D", VAWorld, icon)
	add_custom_type("VADefaultMaterial", "Node", VADefaultMaterial, icon)
	add_custom_type("VACustomMaterial", "Node", VACustomMaterial, icon)

	world_gizmo_plugin = VAWorldGizmoPlugin.new()
	add_node_3d_gizmo_plugin(world_gizmo_plugin)

	debugger_plugin = VADebuggerPlugin.new()
	add_debugger_plugin(debugger_plugin)

	material_inspector_plugin = VAMaterialInspectorPlugin.new()
	material_inspector_plugin.set_debugger_plugin(debugger_plugin)
	add_inspector_plugin(material_inspector_plugin)

	device_refresh_inspector_plugin = VADeviceRefreshInspectorPlugin.new()
	add_inspector_plugin(device_refresh_inspector_plugin)

	inspector_tooltip_plugin = VAInspectorTooltipPlugin.new()
	add_inspector_plugin(inspector_tooltip_plugin)

	conversion_context_menu_plugin = VAConversionContextMenuPlugin.new()
	add_context_menu_plugin(EditorContextMenuPlugin.CONTEXT_SLOT_SCENE_TREE, conversion_context_menu_plugin)

	_setup_project()

	# Register audio/vaudio/* Project Settings
	_register_project_settings()

	if not ProjectSettings.settings_changed.is_connected(_on_settings_changed):
		ProjectSettings.settings_changed.connect(_on_settings_changed)

	print("[vaudio-godot-mono-openal-3d] Vercidium Audio (vaudio) plugin enabled")

func _exit_tree():
	remove_custom_type("VAEmitter")
	remove_custom_type("VAListener")
	remove_custom_type("ALSource")
	remove_custom_type("ALSource3D")
	remove_custom_type("VAStreamSource")
	remove_custom_type("VAInputStreamSource")
	remove_custom_type("VANetworkedStreamSource")
	remove_custom_type("VASource")
	remove_custom_type("VASourceLeech")
	remove_custom_type("ALSourceRelative")
	remove_custom_type("VASourceRelative")
	remove_custom_type("VASourceAmbient")
	remove_custom_type("VAVisualisation")
	remove_custom_type("VAWorld")
	remove_custom_type("VADefaultMaterial")
	remove_custom_type("VACustomMaterial")

	if world_gizmo_plugin:
		remove_node_3d_gizmo_plugin(world_gizmo_plugin)
		world_gizmo_plugin = null

	if material_inspector_plugin:
		remove_inspector_plugin(material_inspector_plugin)
		material_inspector_plugin = null

	if device_refresh_inspector_plugin:
		remove_inspector_plugin(device_refresh_inspector_plugin)
		device_refresh_inspector_plugin = null

	if inspector_tooltip_plugin:
		remove_inspector_plugin(inspector_tooltip_plugin)
		inspector_tooltip_plugin = null

	if debugger_plugin:
		remove_debugger_plugin(debugger_plugin)
		debugger_plugin = null

	if conversion_context_menu_plugin:
		remove_context_menu_plugin(conversion_context_menu_plugin)
		conversion_context_menu_plugin = null

	if ProjectSettings.settings_changed.is_connected(_on_settings_changed):
		ProjectSettings.settings_changed.disconnect(_on_settings_changed)

	print("Vercidium Audio (vaudio-godot-mono-openal-3d) plugin disabled")

var _setup_done := false

func _on_settings_changed():
	if not _setup_done:
		_setup_project()

func _setup_project():
	var project_name = ProjectSettings.get_setting("application/config/name")
	var csproj_path = "res://%s.csproj" % project_name

	if not FileAccess.file_exists(csproj_path):
		push_error("[vaudio-godot-mono-openal-3d] No C# solution found. This plugin requires C# - please create a C# solution (Project → Tools → C# → Create C# Solution) and then re-enable this plugin")
		return

	var file = FileAccess.open(csproj_path, FileAccess.READ)
	if not file:
		return

	var content = file.get_as_text()
	file.close()

	_setup_done = true

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

	if dll_exists:
		print("[vaudio-godot-mono-openal-3d] vaudio.dll found")
	else:
		push_error("[vaudio-godot-mono-openal-3d] vaudio.dll not found - please copy your vaudio.dll into %s, then disable and enable the Vercidium Audio plugin" % ProjectSettings.globalize_path(dll_res_path.get_base_dir()))

	_copy_dll()

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

func _register_project_settings():
	# output_device: stored as DEFAULT_DEVICE_LABEL, not "", so the strict PROPERTY_HINT_ENUM
	# dropdown below always has a current value among its own entries.
	# ALManager.cs translates DEFAULT_DEVICE_LABEL back to "" ("driver default") when it reads
	# this setting, and rebuilds the hint_string below (via ProjectSettings.AddPropertyInfo)
	# once the real device list is known from OpenAL - registered with just the default label
	# here since GDScript has no OpenAL binding of its own to enumerate devices this early.
	if not ProjectSettings.has_setting("audio/vaudio/output_device"):
		ProjectSettings.set_setting("audio/vaudio/output_device", DEFAULT_DEVICE_LABEL)

	ProjectSettings.set_initial_value("audio/vaudio/output_device", DEFAULT_DEVICE_LABEL)

	ProjectSettings.add_property_info({
		"name": "audio/vaudio/output_device",
		"type": TYPE_STRING,
		"hint": PROPERTY_HINT_ENUM,
		"hint_string": DEFAULT_DEVICE_LABEL,
	})

	# max_reverb_sends: dev-only setting (not end-user-facing), default 1
	if not ProjectSettings.has_setting("audio/vaudio/max_reverb_sends"):
		ProjectSettings.set_setting("audio/vaudio/max_reverb_sends", 1)

	ProjectSettings.set_initial_value("audio/vaudio/max_reverb_sends", 1)

	ProjectSettings.add_property_info({
		"name": "audio/vaudio/max_reverb_sends",
		"type": TYPE_INT,
		"hint": PROPERTY_HINT_RANGE,
		"hint_string": "1,16,or_greater",
	})

	# sample_rate: 0 means "driver default" - never shown to the user as 0.
	if not ProjectSettings.has_setting("audio/vaudio/sample_rate"):
		ProjectSettings.set_setting("audio/vaudio/sample_rate", 0)

	ProjectSettings.set_initial_value("audio/vaudio/sample_rate", 0)

	ProjectSettings.add_property_info({
		"name": "audio/vaudio/sample_rate",
		"type": TYPE_INT,
		"hint": PROPERTY_HINT_ENUM,
		"hint_string": "System Default:0,22050,44100,48000,96000",
	})

	# hrtf_enabled: default true
	if not ProjectSettings.has_setting("audio/vaudio/hrtf_enabled"):
		ProjectSettings.set_setting("audio/vaudio/hrtf_enabled", true)

	ProjectSettings.set_initial_value("audio/vaudio/hrtf_enabled", true)

	# max_mono_sources/max_stereo_sources: project-level settings set by the developer, matching
	# the native Godot plugin's register_types.cpp - read once at device-open time, can't be
	# changed at runtime.
	if not ProjectSettings.has_setting("audio/vaudio/max_mono_sources"):
		ProjectSettings.set_setting("audio/vaudio/max_mono_sources", 16)

	ProjectSettings.set_initial_value("audio/vaudio/max_mono_sources", 16)

	ProjectSettings.add_property_info({
		"name": "audio/vaudio/max_mono_sources",
		"type": TYPE_INT,
		"hint": PROPERTY_HINT_RANGE,
		"hint_string": "0,256,or_greater",
	})

	if not ProjectSettings.has_setting("audio/vaudio/max_stereo_sources"):
		ProjectSettings.set_setting("audio/vaudio/max_stereo_sources", 240)

	ProjectSettings.set_initial_value("audio/vaudio/max_stereo_sources", 240)

	ProjectSettings.add_property_info({
		"name": "audio/vaudio/max_stereo_sources",
		"type": TYPE_INT,
		"hint": PROPERTY_HINT_RANGE,
		"hint_string": "0,256,or_greater",
	})