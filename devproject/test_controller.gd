extends Node3D

# Root script of both test_scene.tscn and reverb_room.tscn. Run with `-- --test` to run the scenarios in res://scenarios and then quit - see scenarios/lib/runner.gd for the options
const RUNNER := "res://scenarios/lib/runner.gd"
const VA_ADAPTER := "res://scenarios/lib/va.gd"

const EAR_HEIGHT := 1.0

@onready var camera: Camera3D = $Camera3D
@onready var listener: Node3D = $Listener
@onready var source: Node3D = $VASource
@onready var world: Node = $VAWorld

var test_mode := OS.get_cmdline_user_args().has("--test")

# Runs before VAWorld._enter_tree (parents enter the tree first), which creates the world and would otherwise show the debug window straight away
func _enter_tree() -> void:
	if test_mode and not OS.get_cmdline_user_args().has("--debugwindow"):
		load(VA_ADAPTER).set_value($VAWorld, "rendering_enabled", false)
	if test_mode and OS.get_cmdline_user_args().has("--mute"):
		load(VA_ADAPTER).set_value($VAWorld, "master_volume", 0.0)

func _ready() -> void:
	# The runner outlives scene changes, so only the first scene starts it
	if test_mode and not get_tree().root.has_node("TestRunner"):
		var runner: Node = load(RUNNER).new()
		runner.name = "TestRunner"
		get_tree().root.add_child.call_deferred(runner)

func _process(_delta: float) -> void:
	if test_mode:
		return

	var mouse := get_viewport().get_mouse_position()
	var target = Plane(Vector3.UP, EAR_HEIGHT).intersects_ray(camera.project_ray_origin(mouse), camera.project_ray_normal(mouse))
	if target == null:
		return

	if Input.is_mouse_button_pressed(MOUSE_BUTTON_RIGHT):
		source.global_position = target
	else:
		listener.global_position = target
