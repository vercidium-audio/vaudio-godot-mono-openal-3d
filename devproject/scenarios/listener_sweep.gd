extends "res://scenarios/lib/scenario.gd"

# Sweeps the listener around the scene - the original smoke test, kept as a baseline for the other scenarios

const EAR_HEIGHT := 1.0

var start_position: Vector3

func setup() -> void:
	start_position = root.listener.global_position

func run() -> void:
	if listen:
		print(">> listener sweeping around the scene - expect muffling to change as walls pass between listener and source")

	var elapsed := 0.0
	var end := duration(5.0)

	while elapsed < end:
		await root.get_tree().process_frame
		elapsed += root.get_process_delta_time()
		root.listener.global_position = Vector3(cos(elapsed * 2.0) * 22.5, EAR_HEIGHT + sin(elapsed * 5.0) * 0.5, sin(elapsed * 3.0) * 12.5)

func teardown() -> void:
	root.listener.global_position = start_position
