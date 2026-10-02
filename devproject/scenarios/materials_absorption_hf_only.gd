extends "res://scenarios/lib/scenario.gd"

# Keeps the reverb room's LF absorption fixed and raises only HF absorption - the reverb should get darker, not shorter. The world only reports one broadband decay time, so "not shorter" is checked against a control step that absorbs both bands equally

const ABSORPTION_LF := 0.1

const STEPS := [
	[0.05, "HF absorption 0.05 - expect a bright tail"],
	[0.5, "HF absorption 0.5 - expect the tail to get darker but stay long"],
	[0.95, "HF absorption 0.95 - expect a dull, boomy tail that still rings"],
]

const CONTROL_ABSORPTION := 0.95

var material: Node
var original_lf: float
var original_hf: float

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	material = root.world.get_node("RoomMaterial")
	original_lf = VA.get_value(material, "absorption_lf")
	original_hf = VA.get_value(material, "absorption_hf")
	VA.set_value(material, "absorption_lf", ABSORPTION_LF)
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	var results := []

	for s in STEPS:
		VA.set_value(material, "absorption_hf", s[0])
		var result := await measure(s[1])
		if result.is_empty():
			fail("source isn't in a grouped reverb at HF absorption %.2f" % s[0])
			return
		print("[devproject] HF absorption %.2f -> reverb gain LF %.4f HF %.4f, decay time %.3f" % [s[0], result.gain_lf, result.gain_hf, result.decay])
		results.append(result)

	VA.set_value(material, "absorption_lf", CONTROL_ABSORPTION)
	var control := await measure("control: LF absorption 0.95 as well - expect the tail to vanish, unlike the step before")
	if control.is_empty():
		fail("source isn't in a grouped reverb in the control step")
		return
	print("[devproject] control, both bands %.2f -> decay time %.3f" % [CONTROL_ABSORPTION, control.decay])

	var first: Dictionary = results[0]
	var last: Dictionary = results[-1]

	for i in range(1, results.size()):
		check(results[i].ratio < results[i - 1].ratio, "reverb HF/LF ratio didn't decrease from HF absorption %.2f (%.4f) to %.2f (%.4f)" % [STEPS[i - 1][0], results[i - 1].ratio, STEPS[i][0], results[i].ratio])

	check(last.ratio < first.ratio * 0.1, "reverb HF/LF ratio only fell from %.4f to %.4f" % [first.ratio, last.ratio])
	check(last.gain_lf >= first.gain_lf * 0.9, "reverb LF gain fell from %.4f to %.4f with LF absorption unchanged" % [first.gain_lf, last.gain_lf])
	check(last.decay > control.decay * 3.0, "HF-only absorption shortened the tail almost as much as absorbing both bands (%.3f vs %.3f)" % [last.decay, control.decay])

# Returns gain_lf/gain_hf/ratio/decay for the source's grouped reverb, or an empty dictionary if it isn't grouped
func measure(banner: String) -> Dictionary:
	await wait_raytraced(SETTLE_PASSES)
	await step(banner, 1.0)

	var index := VA.grouped_eax_index(root.source)
	if index < 0:
		return {}

	var gain_lf := VA.grouped_eax_gain_lf(root.world, index)
	var gain_hf := VA.grouped_eax_gain_hf(root.world, index)
	return {
		"gain_lf": gain_lf,
		"gain_hf": gain_hf,
		"ratio": gain_hf / gain_lf if gain_lf > 0.0 else 0.0,
		"decay": VA.grouped_eax_decay_time(root.world, index),
	}

func teardown() -> void:
	VA.set_value(material, "absorption_lf", original_lf)
	VA.set_value(material, "absorption_hf", original_hf)
