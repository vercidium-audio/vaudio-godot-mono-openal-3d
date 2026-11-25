# AI-created

@tool
extends EditorPlugin

const VAEmitter = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VAEmitter.cs")
const VAListener = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VAListener.cs")
const VAPlayer = preload("res://addons/vaudio-godot-mono-openal-3d/common/player/VAPlayer.cs")
const VAPlayer3D = preload("res://addons/vaudio-godot-mono-openal-3d/player/VAPlayer3D.cs")
const VAStreamSource = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VAStreamSource.cs")
const VAInputStreamSource = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VAInputStreamSource.cs")
const VANetworkedStreamSource = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VANetworkedStreamSource.cs")
const VASource = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VASource.cs")
const VASourceLeech = preload("res://addons/vaudio-godot-mono-openal-3d/common/nodes/VASourceLeech.cs")

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
const VABackendExportPlugin = preload("res://addons/vaudio-godot-mono-openal-3d/common/editor/VABackendExportPlugin.gd")
const VAProjectSettings = preload("res://addons/vaudio-godot-mono-openal-3d/common/editor/VAProjectSettings.gd")

var world_gizmo_plugin
var node_gizmo_plugin
var material_inspector_plugin
var material_properties_inspector_plugin
var conversion_context_menu_plugin
var debugger_plugin
var debugger_singleton
var device_refresh_inspector_plugin
var inspector_tooltip_plugin
var backend_export_plugin

const DEBUGGER_PLUGIN_SINGLETON_NAME = "VADebuggerPlugin"

func _enter_tree():
	var icon = preload("res://addons/vaudio-godot-mono-openal-3d/icons/vercidium.svg")
	var iconPlayer = preload("res://addons/vaudio-godot-mono-openal-3d/icons/vercidium_player.svg")

	add_custom_type("VAEmitter", "Node3D", VAEmitter, icon)
	add_custom_type("VAListener", "Node3D", VAListener, icon)

	add_custom_type("VAPlayer", "Node", VAPlayer, iconPlayer)
	add_custom_type("VAPlayer3D", "Node3D", VAPlayer3D, iconPlayer)
	add_custom_type("VAStreamSource", "Node3D", VAStreamSource, iconPlayer)
	add_custom_type("VAInputStreamSource", "Node3D", VAInputStreamSource, iconPlayer)
	add_custom_type("VANetworkedStreamSource", "Node3D", VANetworkedStreamSource, iconPlayer)
	add_custom_type("VASource", "Node3D", VASource, iconPlayer)
	add_custom_type("VASourceLeech", "Node3D", VASourceLeech, iconPlayer)

	add_custom_type("VASourceRelative", "Node", VASourceRelative, iconPlayer)
	add_custom_type("VASourceAmbient", "Node", VASourceAmbient, iconPlayer)

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

	backend_export_plugin = VABackendExportPlugin.new()
	add_export_plugin(backend_export_plugin)

	conversion_context_menu_plugin = VAConversionContextMenuPlugin.new()
	add_context_menu_plugin(EditorContextMenuPlugin.CONTEXT_SLOT_SCENE_TREE, conversion_context_menu_plugin)

	# Register audio/vaudio/* Project Settings
	VAProjectSettings.register()
	ProjectSettings.settings_changed.connect(VAProjectSettings.update_backend_visibility)

	# Populate the output_device dropdown with the backend's real device list
	VAWorld.RefreshOutputDeviceList()

	print("[vaudio-godot-mono-openal-3d] Vercidium Audio (vaudio) plugin enabled")

func _exit_tree():
	if ProjectSettings.settings_changed.is_connected(VAProjectSettings.update_backend_visibility):
		ProjectSettings.settings_changed.disconnect(VAProjectSettings.update_backend_visibility)

	remove_custom_type("VAEmitter")
	remove_custom_type("VAListener")
	remove_custom_type("VAPlayer")
	remove_custom_type("VAPlayer3D")
	remove_custom_type("VAStreamSource")
	remove_custom_type("VAInputStreamSource")
	remove_custom_type("VANetworkedStreamSource")
	remove_custom_type("VASource")
	remove_custom_type("VASourceLeech")
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

	if backend_export_plugin:
		remove_export_plugin(backend_export_plugin)
		backend_export_plugin = null

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
