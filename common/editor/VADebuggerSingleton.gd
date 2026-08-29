@tool
extends Object

var _debugger_plugin: EditorDebuggerPlugin

func _init(debugger_plugin: EditorDebuggerPlugin) -> void:
	_debugger_plugin = debugger_plugin

# Args are dimension-specific and forwarded untouched - see VADebuggerPlugin.sync_viewport_camera.
func sync_viewport_camera(a, b, c) -> void:
	if _debugger_plugin != null:
		_debugger_plugin.sync_viewport_camera(a, b, c)
