extends "res://scenarios/lib/scenario.gd"

# Checks reverb_room.tscn is actually enclosed and raytraced before the material/reverb scenarios rely on it

func _init() -> void:
	scene = REVERB_ROOM

func run() -> void:
	check(root.scene_file_path == REVERB_ROOM, "runner didn't switch to %s" % REVERB_ROOM)

	await wait_raytraced_by_listener(root.source)
	await wait_raytraced()
	await step("reverb room at its default absorption - expect a clear, fairly long tail", 2.0)

	var decay := VA.decay_time(root.world, root.source)
	var hf := VA.muffling_hf(root.source)

	check(VA.grouped_eax_index(root.source) >= 0, "source isn't in a grouped reverb")
	check(decay > 0.0, "decay time %f in an enclosed room" % decay)
	check(hf > 0.5, "muffling HF %f with line of sight inside the room" % hf)

	print("[devproject] reverb room decay time %.3f, muffling HF %.3f" % [decay, hf])
