# AI-created

extends Node

# Runs the scenarios in res://scenarios. Lives directly under the tree root rather than in the scene, so it survives switching to each scenario's scene. Started by test_controller.gd in --test mode:
#   --scenarios=a,b   only run these scenarios, in this order (default: every top-level script in res://scenarios, alphabetically)
#   --listen          stretch each step and print what to listen for
#   --debugwindow     leave VAWorld.rendering_enabled on (dev builds) - off by default so test runs don't open debug windows. Applied by test_controller.gd's _enter_tree, before the world is created
#   --mute            set VAWorld.master_volume (the OpenAL listener gain) to 0 so headless runs are silent. Also applied by test_controller.gd's _enter_tree
#   --backend=name    the audio backend the project runs on (default openal; godottest --backend selects it via override.cfg). Scenarios whose requires_backends doesn't include it are skipped
#   --backend-smoke   only run scenarios with backend_smoke set (the playback-related ones), for checking a non-default backend
# Skipped scenarios print "[devproject] SKIP name: reason", even when named in --scenarios=, and the run fails if every scenario was skipped
# A scenario also fails if the audio backend's error count (VA.backend_error_count) went up while it ran, so "no backend errors" is checked on every backend

const VA := preload("res://scenarios/lib/va.gd")

const TEST_PASSED_MARKER := "[devproject] Test passed"
const ARCH_MARKER := "[devproject] arch"
const SCENARIOS_DIR := "res://scenarios"
const DEFAULT_BACKEND := "openal"

var current_scenario := ""
var current_deadline_msec := 0
var error_capture: Logger = preload("res://scenarios/lib/error_capture.gd").new()

func _ready() -> void:
	# Keep running (and keep the watchdog ticking) if a scenario pauses the tree
	process_mode = Node.PROCESS_MODE_ALWAYS
	OS.add_logger(error_capture)
	# godottest checks this, so a Mac --x86_64 run can't silently fall back to arm64
	print("%s %s" % [ARCH_MARKER, Engine.get_architecture_name()])
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
	var backend_smoke := args.has("--backend-smoke")
	var backend := DEFAULT_BACKEND
	var names: Array[String] = []

	# Only top-level scripts are scenarios - shared helpers live in scenarios/lib. ResourceLoader rather than DirAccess, since an exported game stores them as .gd.remap
	for file in ResourceLoader.list_directory(SCENARIOS_DIR):
		if file.get_extension() == "gd":
			names.append(file.get_basename())
	names.sort()

	for arg in args:
		if arg.begins_with("--scenarios="):
			names.assign(arg.trim_prefix("--scenarios=").split(",", false))
		elif arg.begins_with("--backend="):
			backend = arg.trim_prefix("--backend=")

	var failed := 0
	var to_run: Array = []

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
		var skip_reason := _skip_reason(scenario, backend, backend_smoke)
		if skip_reason.is_empty():
			to_run.append([scenario_name, scenario])
		else:
			print("[devproject] SKIP %s: %s" % [scenario_name, skip_reason])

	if to_run.is_empty() and failed == 0:
		push_error("[devproject] FAIL no scenarios to run in %s (backend %s%s)" % [SCENARIOS_DIR, backend, ", backend smoke only" if backend_smoke else ""])
		failed += 1

	for entry in to_run:
		var scenario_name: String = entry[0]
		var scenario = entry[1]
		scenario.listen = listen

		print("[devproject] RUN %s" % scenario_name)
		current_scenario = scenario_name
		current_deadline_msec = Time.get_ticks_msec() + int(scenario.duration(scenario.timeout_seconds) * 1000.0)

		scenario.root = await _open_scene(scenario.scene)
		error_capture.take()
		var backend_errors_before := _backend_error_count()
		await scenario.setup()
		await scenario.run()
		await scenario.teardown()

		current_scenario = ""
		_check_errors(scenario, error_capture.take())
		var backend_errors := _backend_error_count() - backend_errors_before
		if backend_errors > 0:
			scenario.fail("the %s backend logged %d error(s)" % [backend, backend_errors])

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

# Why a scenario doesn't run on this backend/pass, or empty if it does
func _skip_reason(scenario, backend: String, backend_smoke: bool) -> String:
	if not scenario.requires_backends.is_empty() and not scenario.requires_backends.has(backend):
		return "requires backend %s, running on %s" % [", ".join(scenario.requires_backends), backend]
	if backend_smoke and not scenario.backend_smoke:
		return "not a backend smoke scenario"
	return ""

# Read through the current scene's world rather than scenario.root's, since a scenario may change scenes and free its root
func _backend_error_count() -> int:
	return VA.backend_error_count(get_tree().current_scene.world)

# Every expected error/warning must have been logged, and any other error fails the scenario. Unexpected warnings are allowed, same as in godottest.cs
func _check_errors(scenario, logged: Dictionary) -> void:
	var errors: PackedStringArray = logged.errors
	var warnings: PackedStringArray = logged.warnings

	for expected in scenario.expected_errors:
		if not Array(errors).any(func(e): return e.contains(expected)):
			scenario.fail("expected an error containing \"%s\", but none was logged" % expected)

	for i in scenario.expected_warnings.size():
		var expected: String = scenario.expected_warnings[i]
		var expected_count: int = scenario.expected_warning_counts[i]
		var logged_count := Array(warnings).filter(func(w): return w.contains(expected)).size()
		if logged_count == 0:
			scenario.fail("expected a warning containing \"%s\", but none was logged" % expected)
		elif expected_count >= 0 and logged_count != expected_count:
			scenario.fail("expected a warning containing \"%s\" %d time(s), but it was logged %d times" % [expected, expected_count, logged_count])

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
