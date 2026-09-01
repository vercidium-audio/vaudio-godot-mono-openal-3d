@tool
extends EditorPlugin

const VAEmitter = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VAEmitter.cs")
const VAListener = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VAListener.cs")
const AudioSource = preload("res://addons/vaudio-godot-mono-openal-3d/common/audionodes/AudioSource.cs")
const AudioSource3D = preload("res://addons/vaudio-godot-mono-openal-3d/audionodes/AudioSource3D.cs")
const VAStreamSource = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VAStreamSource.cs")
const VAInputStreamSource = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VAInputStreamSource.cs")
const VANetworkedStreamSource = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VANetworkedStreamSource.cs")
const VASource = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VASource.cs")
const VASourceLeech = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VASourceLeech.cs")

const AudioSourceRelative = preload("res://addons/vaudio-godot-mono-openal-3d/audionodes/AudioSourceRelative.cs")
const VASourceRelative = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VASourceRelative.cs")
const VASourceAmbient = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VASourceAmbient.cs")
const VAVisualisation = preload("res://addons/vaudio-godot-mono-openal-3d/nodes/VAVisualisation.cs")
const VAWorld = preload("res://addons/vaudio-godot-mono-openal-3d/world/VAWorld.cs")
const VADefaultMaterial = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VADefaultMaterial.cs")
const VACustomMaterial = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VACustomMaterial.cs")

const VAWorldGizmoPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/editor/VAWorldGizmoPlugin.gd")
const VANodeGizmoPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/editor/VANodeGizmoPlugin.gd")
const VAMaterialInspectorPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/common/editor/VAMaterialInspectorPlugin.gd")
const VAMaterialPropertiesInspectorPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/common/editor/VAMaterialPropertiesInspectorPlugin.gd")
const VAConversionContextMenuPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/common/editor/VAConversionContextMenuPlugin.gd")
const VADebuggerPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/common/editor/VADebuggerPlugin.gd")
const VADebuggerSingleton = preload("res://addons/vaudio-godot-mono-openal-3d/common/editor/VADebuggerSingleton.gd")
const VADeviceRefreshInspectorPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/common/editor/VADeviceRefreshInspectorPlugin.gd")
const VAInspectorTooltipPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/common/editor/VAInspectorTooltipPlugin.gd")

var world_gizmo_plugin
var node_gizmo_plugin
var material_inspector_plugin
var material_properties_inspector_plugin
var conversion_context_menu_plugin
var debugger_plugin
var debugger_singleton
var device_refresh_inspector_plugin
var inspector_tooltip_plugin

const DEBUGGER_PLUGIN_SINGLETON_NAME = "VADebuggerPlugin"

# "audio/vaudio/*" Project Settings
const DEFAULT_DEVICE_LABEL = "System Default"

func _enter_tree():
	var icon = preload("res://addons/vaudio-godot-mono-openal-3d/icons/vercidium.svg")
	var iconAL = preload("res://addons/vaudio-godot-mono-openal-3d/icons/vercidium_al.svg")

	add_custom_type("VAEmitter", "Node3D", VAEmitter, icon)
	add_custom_type("VAListener", "Node3D", VAListener, icon)

	add_custom_type("AudioSource", "Node", AudioSource, iconAL)
	add_custom_type("AudioSource3D", "Node3D", AudioSource3D, iconAL)
	add_custom_type("VAStreamSource", "Node3D", VAStreamSource, iconAL)
	add_custom_type("VAInputStreamSource", "Node3D", VAInputStreamSource, iconAL)
	add_custom_type("VANetworkedStreamSource", "Node3D", VANetworkedStreamSource, iconAL)
	add_custom_type("VASource", "Node3D", VASource, iconAL)
	add_custom_type("VASourceLeech", "Node3D", VASourceLeech, iconAL)

	add_custom_type("AudioSourceRelative", "Node", AudioSourceRelative, iconAL)
	add_custom_type("VASourceRelative", "Node", VASourceRelative, iconAL)
	add_custom_type("VASourceAmbient", "Node", VASourceAmbient, iconAL)

	add_custom_type("VAVisualisation", "Node3D", VAVisualisation, icon)
	add_custom_type("VAWorld", "Node3D", VAWorld, icon)
	add_custom_type("VADefaultMaterial", "Node", VADefaultMaterial, icon)
	add_custom_type("VACustomMaterial", "Node", VACustomMaterial, icon)

	world_gizmo_plugin = VAWorldGizmoPlugin.new()
	add_node_3d_gizmo_plugin(world_gizmo_plugin)

	node_gizmo_plugin = VANodeGizmoPlugin.new()
	add_node_3d_gizmo_plugin(node_gizmo_plugin)

	debugger_plugin = VADebuggerPlugin.new()
	add_debugger_plugin(debugger_plugin)

	debugger_singleton = VADebuggerSingleton.new(debugger_plugin)
	Engine.register_singleton(DEBUGGER_PLUGIN_SINGLETON_NAME, debugger_singleton)

	material_inspector_plugin = VAMaterialInspectorPlugin.new()
	material_inspector_plugin.set_debugger_plugin(debugger_plugin)
	add_inspector_plugin(material_inspector_plugin)

	material_properties_inspector_plugin = VAMaterialPropertiesInspectorPlugin.new()
	material_properties_inspector_plugin.set_debugger_plugin(debugger_plugin)
	add_inspector_plugin(material_properties_inspector_plugin)

	device_refresh_inspector_plugin = VADeviceRefreshInspectorPlugin.new()
	add_inspector_plugin(device_refresh_inspector_plugin)

	inspector_tooltip_plugin = VAInspectorTooltipPlugin.new()
	add_inspector_plugin(inspector_tooltip_plugin)

	conversion_context_menu_plugin = VAConversionContextMenuPlugin.new()
	add_context_menu_plugin(EditorContextMenuPlugin.CONTEXT_SLOT_SCENE_TREE, conversion_context_menu_plugin)

	# Register audio/vaudio/* Project Settings
	_register_project_settings()

	print("[vaudio-godot-mono-openal-3d] Vercidium Audio (vaudio) plugin enabled")

func _exit_tree():
	remove_custom_type("VAEmitter")
	remove_custom_type("VAListener")
	remove_custom_type("AudioSource")
	remove_custom_type("AudioSource3D")
	remove_custom_type("VAStreamSource")
	remove_custom_type("VAInputStreamSource")
	remove_custom_type("VANetworkedStreamSource")
	remove_custom_type("VASource")
	remove_custom_type("VASourceLeech")
	remove_custom_type("AudioSourceRelative")
	remove_custom_type("VASourceRelative")
	remove_custom_type("VASourceAmbient")
	remove_custom_type("VAVisualisation")
	remove_custom_type("VAWorld")
	remove_custom_type("VADefaultMaterial")
	remove_custom_type("VACustomMaterial")

	if world_gizmo_plugin:
		remove_node_3d_gizmo_plugin(world_gizmo_plugin)
		world_gizmo_plugin = null

	if node_gizmo_plugin:
		remove_node_3d_gizmo_plugin(node_gizmo_plugin)
		node_gizmo_plugin = null

	if material_inspector_plugin:
		remove_inspector_plugin(material_inspector_plugin)
		material_inspector_plugin = null

	if material_properties_inspector_plugin:
		remove_inspector_plugin(material_properties_inspector_plugin)
		material_properties_inspector_plugin = null

	if device_refresh_inspector_plugin:
		remove_inspector_plugin(device_refresh_inspector_plugin)
		device_refresh_inspector_plugin = null

	if inspector_tooltip_plugin:
		remove_inspector_plugin(inspector_tooltip_plugin)
		inspector_tooltip_plugin = null

	if Engine.has_singleton(DEBUGGER_PLUGIN_SINGLETON_NAME):
		Engine.unregister_singleton(DEBUGGER_PLUGIN_SINGLETON_NAME)

	if debugger_singleton:
		# Plain Object from .new() - not RefCounted, so it must be freed explicitly.
		debugger_singleton.free()
		debugger_singleton = null

	if debugger_plugin:
		remove_debugger_plugin(debugger_plugin)
		debugger_plugin = null

	if conversion_context_menu_plugin:
		remove_context_menu_plugin(conversion_context_menu_plugin)
		conversion_context_menu_plugin = null

	print("Vercidium Audio (vaudio-godot-mono-openal-3d) plugin disabled")

func _register_project_settings():
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

	# max_mono_sources/max_stereo_sources: project-level settings set by the developer, matching the native Godot plugin's register_types.cpp
	# read once at device-open time, can't be changed at runtime.
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