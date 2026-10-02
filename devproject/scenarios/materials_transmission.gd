extends "res://scenarios/lib/scenario.gd"

# Splits the reverb room with a sealed brick partition and raises brick's HF transmission via a runtime VADefaultMaterial - more HF should get through the wall. test_scene.tscn isn't used because in 3D enough energy goes over and around its brick wall to saturate the occlusion energy cap, so it reads as unmuffled even when fully opaque

const BRICK := 1

# LF is left wide open so the HF gain isn't capped by it - a blocked LF band zeroes both
const TRANSMISSION_LF := 30.0

# HF transmission as a multiple of the partition's thickness - vaudio's transmission is the distance a ray travels through a material before losing all HF energy
const STEPS := [
	[1.0, "HF transmission = wall thickness - expect bassy, muffled speech through the wall"],
	[2.0, "2x wall thickness - expect a little more clarity"],
	[4.0, "4x wall thickness - expect mostly clear speech"],
	[10.0, "10x wall thickness - expect the wall to barely dull the speech"],
]

var override: Node
var partition: Node

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	await wait_raytraced_by_listener(root.source)

	override = VA.create_node(root.world, "VADefaultMaterial")
	VA.set_value(override, "material_type", BRICK)
	VA.set_value(override, "transmission_lf", TRANSMISSION_LF)
	root.world.add_child(override)

	partition = DIM.add_reverb_room_partition(root, "brick")

func run() -> void:
	var gains: Array[float] = []

	for s in STEPS:
		VA.set_value(override, "transmission_hf", DIM.PARTITION_THICKNESS_METRES * s[0])
		await wait_raytraced(SETTLE_PASSES)
		await step(s[1], 1.0)

		var lf := VA.muffling_lf(root.source)
		var hf := VA.muffling_hf(root.source)
		print("[devproject] brick transmission_hf %.2fx wall -> muffling LF %.4f HF %.4f" % [s[0], lf, hf])
		check(lf > 0.9, "muffling LF %.4f at %.0fx HF transmission, but LF transmission is wide open" % [lf, s[0]])
		gains.append(hf)

	for i in range(1, gains.size()):
		check(gains[i] > gains[i - 1], "muffling HF didn't increase from %.0fx (%.4f) to %.0fx (%.4f) wall thickness" % [STEPS[i - 1][0], gains[i - 1], STEPS[i][0], gains[i]])

	check(gains[0] < 0.05, "muffling HF %.4f with HF transmission equal to the wall thickness, expected almost fully muffled" % gains[0])
	check(gains[-1] > 0.9, "muffling HF %.4f at 10x wall thickness, expected almost unmuffled" % gains[-1])

# Freeing the VADefaultMaterial restores brick's built-in values (asserted by materials_default_override)
func teardown() -> void:
	partition.queue_free()
	override.queue_free()
	await wait_raytraced()
