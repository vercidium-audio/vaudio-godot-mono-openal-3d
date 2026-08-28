@tool
extends Object

# Object (not RefCounted) relay registered as the "VADebuggerPlugin" Engine singleton in
# plugin_main.gd, so VAWorld.cs (SendViewportCameraToRunningGame) can reach the real
# VADebuggerPlugin - a VAWorld is instantiated by the user's own scene, not constructed by this
# plugin, so Engine.get_singleton is its only handle. VADebuggerPlugin itself extends
# EditorDebuggerPlugin (RefCounted); Engine.register_singleton warns and will soon error when
# handed a RefCounted, since it stores only a raw pointer that dangles once every Ref is dropped.
# This Object holds a real reference to the debugger plugin and forwards to it, keeping the
# singleton non-RefCounted while the debugger plugin's lifetime stays owned by plugin_main.gd
# (its member var + add_debugger_plugin).

var _debugger_plugin: EditorDebuggerPlugin

func _init(debugger_plugin: EditorDebuggerPlugin) -> void:
	_debugger_plugin = debugger_plugin

# Args are dimension-specific and forwarded untouched - see VADebuggerPlugin.sync_viewport_camera.
func sync_viewport_camera(a, b, c) -> void:
	if _debugger_plugin != null:
		_debugger_plugin.sync_viewport_camera(a, b, c)
