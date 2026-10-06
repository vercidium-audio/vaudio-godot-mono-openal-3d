extends "res://scenarios/lib/scenario.gd"

# Adds a source on each side of a sealed brick partition, detaches the far one with remove_child() and adds it back, and adds then frees a source in the same frame. Each source has to be muffled according to its own side of the wall, a detached source mustn't disturb the others, and grouped reverb must return to its baseline once they're all gone

const SEED := 20261002
const TOLERANCE := 0.05

var partition: Node
var near: Node
var far: Node
var baseline_eax := 0

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	partition = DIM.add_reverb_room_partition(root, "brick")
	await wait_raytraced_by_listener(root.source)
	await wait_raytraced(SETTLE_PASSES)
	baseline_eax = VA.grouped_eax_count(root.world)

func run() -> void:
	var listener_side = root.source.position
	listener_side.x = -listener_side.x

	near = spawn_source("Near", listener_side, SEED)
	far = spawn_source("Far", root.source.position, SEED + 1)
	await wait_raytraced_by_listener(near)
	await wait_raytraced_by_listener(far)
	check(await wait_playing(near), "near source isn't playing after being added")
	check(await wait_playing(far), "far source isn't playing after being added")

	var near_clear := await measure_muffling("near, listener's side", "source added on the listener's side - expect clear speech", near)
	var far_blocked := await measure_muffling("far, behind wall", "source added behind the wall - expect muffled speech", far)

	root.remove_child(far)
	var near_detached := await measure_muffling("near while far is detached", "far source detached - expect only the near and original voices", near)
	var original_detached := await measure_muffling("original while far is detached", "far source detached - expect the original voice unchanged")

	root.add_child(far)
	await wait_raytraced_by_listener(far)
	check(await wait_playing(far), "far source isn't playing after being added back")
	var far_reAdded := await measure_muffling("far added back", "far source added back behind the wall - expect muffled speech", far)

	# Emitter created and released before the world ever sees it
	var flicker := spawn_source("Flicker", listener_side, SEED + 2)
	flicker.free()
	await wait_raytraced()

	near.queue_free()
	far.queue_free()
	await step("near and far sources freed - expect only the original voice", 1.0)
	var after := await wait_grouped_eax_count(baseline_eax)
	var original := await measure_muffling("original after freeing", "only the original voice left - expect muffled speech")
	print("[devproject] added sources freed -> grouped EAX count %d (baseline %d)" % [after, baseline_eax])

	check(near_clear.y > 0.9, "muffling HF %.4f for the source added on the listener's side, expected almost unmuffled" % near_clear.y)
	check(far_blocked.y < 0.5, "muffling HF %.4f for the source added behind the wall, expected the partition to muffle it" % far_blocked.y)
	check(absf(near_detached.y - near_clear.y) < TOLERANCE, "near source's muffling HF %.4f while the far source was detached differs from %.4f before" % [near_detached.y, near_clear.y])
	check(original_detached.y < 0.5, "original source's muffling HF %.4f while the far source was detached, expected it still muffled behind the wall" % original_detached.y)
	check(absf(far_reAdded.y - far_blocked.y) < TOLERANCE, "far source's muffling HF %.4f after being added back differs from %.4f before it was detached" % [far_reAdded.y, far_blocked.y])
	check(after == baseline_eax, "grouped EAX count %d after freeing the added sources didn't return to the baseline %d" % [after, baseline_eax])
	check(VA.is_raytraced_by_listener(root.source), "original source is no longer raytraced after freeing the added sources")
	check(original.y < 0.5, "original source's muffling HF %.4f after freeing the added sources, expected it still muffled behind the wall" % original.y)

func teardown() -> void:
	for source in [near, far]:
		if is_instance_valid(source):
			source.free()
	partition.queue_free()
	await wait_grouped_eax_count(baseline_eax)
