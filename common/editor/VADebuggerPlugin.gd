@tool
extends EditorDebuggerPlugin

# Relays "Vercidium Audio" material/use-flat-transmission edits made in the Inspector while the
# game is running to the running game's own process - EditorInspectorPlugin controls (see
# VAMaterialInspectorPlugin) only ever run against the editor's local copy of the scene, whose
# VAWorld.world is null (it's only created outside the editor - see VAWorld._EnterTree). Godot's
# debugger protocol is the only bridge between the two processes, so this sends a
# "vaudio:sync_primitive" message to every active session; the running game receives it via an
# EngineDebugger message capture registered in VAWorld (see VAWorldDebugger.cs).
# Mirrors the native (C++ GDExtension) plugin's VADebuggerPlugin.

# node_path is relative to the edited scene's root node, whose name is scene_root_name -
# SceneTree.current_scene isn't reliable here (a running game may add a scene as a plain child
# rather than via change_scene_to_*), so the receiving end searches for scene_root_name anywhere
# under the running game's root instead.
#
# The running game has its own separate copy of this node, whose metadata was never touched by the
# edit that just happened in the editor's local copy - material/use_flat_transmission carry that
# new metadata across so the receiving end can apply it before re-adding the primitive. An empty
# material means "no material metadata" (Air), matching remove_meta in
# VAMaterialInspectorPlugin._on_material_selected; use_flat_transmission is null for the same
# "no metadata, use the default" case, matching _on_use_flat_transmission_toggled.
func sync_primitive(scene_root_name: String, node_path: NodePath, material: String, use_flat_transmission) -> void:
	for session in get_sessions():
		# Only sessions currently attached to a running game can receive messages - a session
		# stays in get_sessions() after the game it was attached to has stopped.
		if session != null and session.is_active():
			session.send_message("vaudio:sync_primitive", [scene_root_name, node_path, material, use_flat_transmission])

# Relays a VADefaultMaterial/VACustomMaterial property edit made in the Inspector while the game
# is running - see VAMaterialPropertiesInspectorPlugin.gd. Received by
# VAWorld.OnSyncMaterialProperties (VAWorldDebugger.cs), which applies the values directly via
# ApplyPropertiesFromEditor - unlike sync_primitive above, there's no metadata to carry across.
# node_name/is_custom_material/material_type/custom_material_name let the receiving end create the
# node itself (mirroring the editor's local copy) if it doesn't exist in the running game yet - see
# VAMaterialPropertiesInspectorPlugin._sync_running_game for why that can happen.
func sync_material_properties(scene_root_name: String, node_path: NodePath, node_name: String,
		is_custom_material: bool, material_type: int, custom_material_name: String,
		absorption_lf: float, absorption_hf: float, scattering: float, transmission_lf: float,
		transmission_hf: float, flat_transmission_lf: float, flat_transmission_hf: float,
		debug_color: Color) -> void:
	for session in get_sessions():
		if session != null and session.is_active():
			session.send_message("vaudio:sync_material_properties", [scene_root_name, node_path,
				node_name, is_custom_material, material_type, custom_material_name, absorption_lf,
				absorption_hf, scattering, transmission_lf, transmission_hf, flat_transmission_lf,
				flat_transmission_hf, debug_color])

# Relays the editor's viewport camera to every active game session, for VAWorld's SyncViewport
# property (see VAWorldProperties.cs/VAWorldDebugger.cs) to mirror into the vaudio debug render
# window. Polled every editor frame by VAWorld.cs itself (via Engine.get_singleton, since this
# instance is registered as a singleton in plugin_main.gd rather than pushed into a consumer the
# way the inspector plugins above are - VAWorld is instantiated by the user's own scene, not
# constructed by this plugin), rather than being driven by an inspector/gizmo edit like
# sync_primitive/sync_material_properties are.
#
# Args are dimension-specific and passed straight through untouched: 3D sends
# (position: Vector3, rotation: Vector3, fov_degrees: float); 2D sends
# (centre: Vector2, rotation: float, zoom: float). The matching VAWorld.OnSyncViewportCamera
# unpacks whichever shape its own dimension sent.
func sync_viewport_camera(a, b, c) -> void:
	for session in get_sessions():
		if session != null and session.is_active():
			session.send_message("vaudio:sync_viewport_camera", [a, b, c])
