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
