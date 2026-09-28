extends Node3D

# Run with `-- --test` to sweep the listener around the scene for a fixed duration and then quit - used by the vaudio package script's headless tests
const TEST_DURATION_SECONDS := 5.0
const TEST_PASSED_MARKER := "[devproject] Test passed"

const EAR_HEIGHT := 1.0

@onready var camera: Camera3D = $Camera3D
@onready var listener: Node3D = $Listener
@onready var source: Node3D = $VASource

var test_mode := OS.get_cmdline_user_args().has("--test")
var test_elapsed := 0.0

func _process(delta: float) -> void:
	if test_mode:
		_process_test(delta)
		return

	var mouse := get_viewport().get_mouse_position()
	var target = Plane(Vector3.UP, EAR_HEIGHT).intersects_ray(camera.project_ray_origin(mouse), camera.project_ray_normal(mouse))
	if target == null:
		return

	if Input.is_mouse_button_pressed(MOUSE_BUTTON_RIGHT):
		source.global_position = target
	else:
		listener.global_position = target

func _process_test(delta: float) -> void:
	test_elapsed += delta
	listener.global_position = Vector3(cos(test_elapsed * 2.0) * 22.5, EAR_HEIGHT + sin(test_elapsed * 5.0) * 0.5, sin(test_elapsed * 3.0) * 12.5)

	if test_elapsed >= TEST_DURATION_SECONDS:
		print("%s after %d frames" % [TEST_PASSED_MARKER, Engine.get_process_frames()])
		get_tree().quit()
