extends RefCounted

# Base class for devproject test scenarios, run by lib/runner.gd. Override setup/run/teardown - each may be a coroutine. teardown must restore the scene so scenarios stay order-independent.

# Hides the C# (PascalCase properties, GainHF) vs native (snake_case, get_muffling_gain_hf()) naming differences, so scenarios are identical across the pair
const VA := preload("res://scenarios/lib/va.gd")

# 2D/3D geometry helpers with matching signatures
const DIM := preload("res://scenarios/lib/dim.gd")

# In --listen mode each step is stretched by this factor so it can be judged by ear
const LISTEN_STRETCH := 4.0

# Scene the runner switches to before setup(), set in _init() to override. Both scenes' roots use test_controller.gd, so root.listener/source/world work in either
const TEST_SCENE := "res://test_scene.tscn"
const REVERB_ROOM := "res://reverb_room.tscn"
var scene := TEST_SCENE

var root: Node
var listen := false
var failure := ""

# Substrings of errors/warnings this scenario deliberately triggers - see expect_error()/expect_warning()
var expected_errors: PackedStringArray = []
var expected_warnings: PackedStringArray = []

# Raytrace passes to wait after a material/ray change before reading reverb results
const SETTLE_PASSES := 10

# Seconds before the runner gives up on this scenario and fails the run, before the listen stretch is applied
var timeout_seconds := 60.0

func setup() -> void:
	pass

func run() -> void:
	pass

func teardown() -> void:
	pass

# Records the first failure only, so the reason printed is the root cause rather than a knock-on effect
func fail(reason: String) -> void:
	if failure.is_empty():
		failure = reason

# Call before triggering an error on purpose. The runner fails the scenario if no error containing this text is logged, and godottest.cs stops treating matching ERROR lines as failures
func expect_error(substring: String) -> void:
	expected_errors.append(substring)
	print("[devproject] EXPECT_ERROR %s" % substring)

# Call before triggering a warning on purpose. The runner fails the scenario if no warning containing this text is logged - warnings never fail a run otherwise, so there's nothing for godottest.cs to ignore
func expect_warning(substring: String) -> void:
	expected_warnings.append(substring)

func check(condition: bool, reason: String) -> void:
	if not condition:
		fail(reason)

func duration(seconds: float) -> float:
	return seconds * LISTEN_STRETCH if listen else seconds

# Waits for a timed step, printing what to listen for first in --listen mode
func step(banner: String, seconds: float) -> void:
	if listen:
		print(">> %s" % banner)
	await root.get_tree().create_timer(duration(seconds)).timeout

func wait_frames(count: int) -> void:
	for i in count:
		await root.get_tree().process_frame

# Waits for fresh results after changing the scene. Two passes, because the pass already in flight when the change was made may have started before it
func wait_raytraced(passes := 2) -> void:
	var target: int = VA.raytrace_count(root.world) + passes
	while VA.raytrace_count(root.world) < target:
		await root.get_tree().process_frame

# Waits for fresh results after a change, then prints and returns the source's (muffling LF, muffling HF). Defaults to the scene's own source
func measure_muffling(label: String, banner: String, source: Node = null) -> Vector2:
	if source == null:
		source = root.source
	await wait_raytraced(SETTLE_PASSES)
	await step(banner, 1.0)
	var muffling := Vector2(VA.muffling_lf(source), VA.muffling_hf(source))
	print("[devproject] %s -> muffling LF %.4f HF %.4f" % [label, muffling.x, muffling.y])
	return muffling

# Adds a looping, autoplaying VASource that plays the scene source's streams. Freed emitters linger until their reverb tails finish - see wait_grouped_eax_count()
func spawn_source(source_name: String, position, scattering_seed: int) -> Node:
	var source := VA.create_node(root.world, "VASource")
	source.name = source_name
	source.position = position
	VA.set_value(source, "streams", VA.get_value(root.source, "streams"))
	VA.set_value(source, "looping", true)
	VA.set_value(source, "autoplay", true)
	VA.set_value(source, "reverb_ray_count", 64)
	VA.set_value(source, "reverb_bounce_count", 64)
	VA.set_value(source, "scattering_seed", scattering_seed)
	root.add_child(source)
	return source

# Freed emitters stay in the world until their reverb tails finish, so this polls for the grouped EAX count to reach target rather than waiting a fixed number of passes. Returns the last count seen
func wait_grouped_eax_count(target: int, timeout_ms := 10000) -> int:
	var started := Time.get_ticks_msec()
	var count := VA.grouped_eax_count(root.world)
	while count != target and Time.get_ticks_msec() - started < timeout_ms:
		await wait_raytraced()
		count = VA.grouped_eax_count(root.world)
	return count

# For sources created or moved at runtime - waits until the listener has actually raytraced them, not just until any pass completes
func wait_raytraced_by_listener(source: Node) -> void:
	while not VA.is_raytraced_by_listener(source):
		await root.get_tree().process_frame

# The Standard plugin autoplays once the source is raytraced, but only starts once its streams finish decoding on a worker thread, so is_playing() can lag a few frames behind. Returns whether it started before the timeout
func wait_playing(source: Node, timeout_ms := 5000) -> bool:
	var started := Time.get_ticks_msec()
	while not VA.is_playing(source) and Time.get_ticks_msec() - started < timeout_ms:
		await root.get_tree().process_frame
	return VA.is_playing(source)
