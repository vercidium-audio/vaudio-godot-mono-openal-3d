@tool
extends EditorPlugin

# Thin bootstrap in front of plugin_main.gd (the real plugin). plugin_main.gd's top-level consts
# preload() every vaudio *.cs node script, and several editor/*.gd files it also preloads do
# `is SomeVaudioType` checks against those C# scripts - if the project has no .csproj yet (or the
# C# assembly hasn't compiled), Godot can't resolve those C# types, and preloading/parsing
# plugin_main.gd blows up with a wall of "Expression is of type Object" parse errors instead of a
# clean message. Checking for a .csproj has to happen before plugin_main.gd (or anything it
# preloads) is ever touched - load() (not preload/const) defers that until _enter_tree, after the
# check below has already passed.
var main_plugin: EditorPlugin

func _enter_tree():
	var project_name = ProjectSettings.get_setting("application/config/name")
	var csproj_path = "res://%s.csproj" % project_name

	if not FileAccess.file_exists(csproj_path):
		push_error("[vaudio-godot-mono-openal-3d] No C# solution found. This plugin requires C# - please create a C# solution (Project → Tools → C# → Create C# Solution) and then re-enable this plugin")
		return

	var main_script = load("res://addons/vaudio-godot-mono-openal-3d/plugin_main.gd")
	main_plugin = main_script.new()
	main_plugin._enter_tree()

func _exit_tree():
	if main_plugin:
		main_plugin._exit_tree()
		main_plugin.queue_free()
		main_plugin = null
