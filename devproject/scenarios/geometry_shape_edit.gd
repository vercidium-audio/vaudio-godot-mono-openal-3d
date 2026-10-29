extends "res://scenarios/lib/scenario.gd"

# Edits the shape resource of a sealing collider (size, radius, points), which doesn't move the node, so the plugin has to pick it up from the shape's changed signal. Open concave shapes use flat transmission, so brick is made fully opaque like geometry_line_edit, and the checks use LF since that silences LF too

const BRICK := 1

var partition: Node
var override: Node

func _init() -> void:
	scene = REVERB_ROOM
	timeout_seconds = 180.0

func setup() -> void:
	override = VA.create_node(root.world, "VADefaultMaterial")
	VA.set_value(override, "material_type", BRICK)
	VA.set_value(override, "flat_transmission_lf", 1.0)
	VA.set_value(override, "flat_transmission_hf", 1.0)
	root.world.add_child(override)
	await wait_raytraced_by_listener(root.source)

func run() -> void:
	for edit in DIM.sealing_shape_edits():
		await check_shape(edit)

func check_shape(edit: Dictionary) -> void:
	var shape_name: String = edit.name
	partition = DIM.add_reverb_room_shape_partition(root, edit.shape, "brick")

	var sealed := await measure_muffling("%s sealed" % shape_name, "%s partition sealing the room - expect muffled speech" % shape_name)
	edit.narrow.call()
	var narrow := await measure_muffling("%s narrowed" % shape_name, "%s shape narrowed to a column - expect clear speech" % shape_name)
	edit.seal.call()
	var resealed := await measure_muffling("%s resealed" % shape_name, "%s shape resized to seal the room again - expect muffled speech" % shape_name)

	check(narrow.x > 0.9, "%s: muffling LF %.4f after narrowing the shape, expected almost unmuffled" % [shape_name, narrow.x])
	check(sealed.x < narrow.x * 0.5, "%s: muffling LF %.4f with the sealing shape isn't below half the narrowed gain %.4f" % [shape_name, sealed.x, narrow.x])
	check(absf(resealed.x - sealed.x) < 0.02, "%s: muffling LF %.4f after resealing the shape didn't return to %.4f" % [shape_name, resealed.x, sealed.x])

	partition.queue_free()
	partition = null
	await wait_raytraced()

func teardown() -> void:
	if partition != null:
		partition.queue_free()
	override.queue_free()
	await wait_raytraced()
