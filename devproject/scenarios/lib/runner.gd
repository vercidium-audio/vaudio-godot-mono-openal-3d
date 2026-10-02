extends Node

# Runs the scenarios in res://scenarios. Lives directly under the tree root rather than in the scene, so it survives switching to each scenario's scene. Started by test_controller.gd in --test mode:
#   --scenarios=a,b   only run these scenarios, in this order (default: every top-level script in res://scenarios, alphabetically)
#   --listen          stretch each step and print what to listen for
#   --debugwindow     leave VAWorld.rendering_enabled on (dev builds) - off by default so test runs don't open debug windows. Applied by test_controller.gd's _enter_tree, before the world is created
#   --mute            set VAWorld.master_volume (the OpenAL listener gain) to 0 so headless runs are silent. Also applied by test_controller.gd's _enter_tree

const TEST_PASSED_MARKER := "[devproject] Test passed"
const SCENARIOS_DIR := "res://scenarios"

var current_scenario := ""
var current_deadline_msec := 0
var error_capture: Logger = preload("res://scenarios/lib/error_capture.gd").new()

func _ready() -> void:
	# Keep running (and keep the watchdog ticking) if a scenario pauses the tree
	process_mode = Node.PROCESS_MODE_ALWAYS
	OS.add_logger(error_capture)
	_run_scenarios()

func _exit_tree() -> void:
	OS.remove_logger(error_capture)

func _process(_delta: float) -> void:
	# A script error inside a scenario coroutine drops it without resuming the runner, so catch that here rather than waiting for the outer timeout
	if not current_scenario.is_empty() and Time.get_ticks_msec() > current_deadline_msec:
		push_error("[devproject] FAIL %s: timed out" % current_scenario)
		get_tree().quit(1)

func _run_scenarios() -> void:
	var args := OS.get_cmdline_user_args()
	var listen := args.has("--listen")
	var names: Array[String] = []

	# Only top-level scripts are scenarios - shared helpers live in scenarios/lib
	for file in DirAccess.get_files_at(SCENARIOS_DIR):
		if file.get_extension() == "gd":
			names.append(file.get_basename())
	names.sort()

	for arg in args:
		if arg.begins_with("--scenarios="):
			names.assign(arg.trim_prefix("--scenarios=").split(",", false))

	var failed := 0

	for scenario_name in names:
		var path := "%s/%s.gd" % [SCENARIOS_DIR, scenario_name]
		if not ResourceLoader.exists(path):
			push_error("[devproject] FAIL %s: no scenario at %s" % [scenario_name, path])
			failed += 1
			continue

		# A script that fails to compile still loads, and .new() would silently run the base class's empty methods
		var script: GDScript = load(path)
		if script == null or not script.can_instantiate():
			push_error("[devproject] FAIL %s: script failed to compile" % scenario_name)
			failed += 1
			continue

		var scenario = script.new()
		scenario.listen = listen

		print("[devproject] RUN %s" % scenario_name)
		current_scenario = scenario_name
		current_deadline_msec = Time.get_ticks_msec() + int(scenario.duration(scenario.timeout_seconds) * 1000.0)

		scenario.root = await _open_scene(scenario.scene)
		error_capture.take()
		await scenario.setup()
		await scenario.run()
		await scenario.teardown()

		current_scenario = ""
		_check_errors(scenario, error_capture.take())

		if scenario.failure.is_empty():
			print("[devproject] PASS %s" % scenario_name)
		else:
			push_error("[devproject] FAIL %s: %s" % [scenario_name, scenario.failure])
			failed += 1

	if failed == 0:
		print("%s after %d frames" % [TEST_PASSED_MARKER, Engine.get_process_frames()])
	else:
		print("[devproject] %d of %d scenario(s) failed" % [failed, names.size()])

	get_tree().quit(0 if failed == 0 else 1)

# Every expected error/warning must have been logged, and any other error fails the scenario. Unexpected warnings are allowed, same as in godottest.cs
func _check_errors(scenario, logged: Dictionary) -> void:
	var errors: PackedStringArray = logged.errors
	var warnings: PackedStringArray = logged.warnings

	for expected in scenario.expected_errors:
		if not Array(errors).any(func(e): return e.contains(expected)):
			scenario.fail("expected an error containing \"%s\", but none was logged" % expected)

	for expected in scenario.expected_warnings:
		if not Array(warnings).any(func(w): return w.contains(expected)):
			scenario.fail("expected a warning containing \"%s\", but none was logged" % expected)

	for error in errors:
		if not Array(scenario.expected_errors).any(func(expected): return error.contains(expected)):
			scenario.fail("unexpected error: %s" % error)

# Reuses the current scene if it's already the right one, so consecutive scenarios in the same scene don't pay for a VAWorld rebuild
func _open_scene(path: String) -> Node:
	var tree := get_tree()
	if tree.current_scene == null or tree.current_scene.scene_file_path != path:
		tree.change_scene_to_file(path)
		await tree.scene_changed
	return tree.current_scene
