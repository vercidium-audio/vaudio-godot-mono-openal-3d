extends "res://scenarios/lib/scenario.gd"

# Steps the reverb room's absorption from almost none to almost total - decay time must drop at every step

const STEPS := [
	[0.01, "absorption 0.01 - expect a long cathedral tail"],
	[0.5, "absorption 0.5 - expect a much shorter tail"],
	[0.99, "absorption 0.99 - expect a dead room, almost no tail"],
]

var material: Node
var original_lf: float
var original_hf: float

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	material = root.world.get_node("RoomMaterial")
	original_lf = VA.get_value(material, "absorption_lf")
	original_hf = VA.get_value(material, "absorption_hf")
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	var decays: Array[float] = []

	for s in STEPS:
		VA.set_value(material, "absorption_lf", s[0])
		VA.set_value(material, "absorption_hf", s[0])
		await wait_raytraced(SETTLE_PASSES)
		await step(s[1], 1.0)

		var decay := VA.decay_time(root.world, root.source)
		print("[devproject] absorption %.2f -> decay time %.3f" % [s[0], decay])
		check(decay >= 0.0, "source isn't in a grouped reverb at absorption %.2f" % s[0])
		decays.append(decay)

	for i in range(1, decays.size()):
		check(decays[i] < decays[i - 1], "decay time didn't decrease from absorption %.2f (%.3f) to %.2f (%.3f)" % [STEPS[i - 1][0], decays[i - 1], STEPS[i][0], decays[i]])

func teardown() -> void:
	VA.set_value(material, "absorption_lf", original_lf)
	VA.set_value(material, "absorption_hf", original_hf)
