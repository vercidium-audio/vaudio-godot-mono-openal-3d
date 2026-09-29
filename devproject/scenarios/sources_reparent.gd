extends "res://scenarios/lib/scenario.gd"

# Reparents a playing source under a pivot on the far side of a sealed brick partition, moves the pivot to the listener's side and back, then reparents the source back to the scene root. The source has to keep playing and being raytraced through each exit/enter, and follow its new parent's transform

const SEED := 20261001
const POSITION_TOLERANCE := 0.01

var partition: Node
var pivot: Node
var rider: Node
var baseline_eax := 0

func _init() -> void:
	scene = REVERB_ROOM

func setup() -> void:
	partition = DIM.add_reverb_room_partition(root, "brick")
	await wait_raytraced_by_listener(root.source)
	await wait_raytraced(SETTLE_PASSES)
	baseline_eax = VA.grouped_eax_count(root.world)
	pivot = DIM.add_pivot(root, root.source.position)
	rider = spawn_source("Rider", root.source.position, SEED)
	await wait_raytraced_by_listener(rider)

func run() -> void:
	var source_side = root.source.position
	var listener_side = source_side
	listener_side.x = -listener_side.x

	var blocked := await measure_muffling("under root, behind wall", "rider behind the wall - expect muffled speech", rider)

	rider.reparent(pivot)
	check_attached("after reparenting under the pivot", pivot, false)
	await wait_raytraced_by_listener(rider)
	var reparented := await measure_muffling("under pivot, behind wall", "rider reparented in place - expect no change", rider)
	check_attached("after settling under the pivot", pivot)

	await slide(listener_side, "pivot carrying the rider round to the listener's side - expect the muffling to sweep out")
	var clear := await measure_muffling("under pivot, listener's side", "rider on the listener's side - expect clear speech", rider)
	check(rider.global_position.distance_to(listener_side) < POSITION_TOLERANCE, "rider at %s after moving its pivot to %s, expected it to follow" % [rider.global_position, listener_side])

	await slide(source_side, "pivot carrying the rider back behind the wall - expect the muffling to sweep in")
	var back := await measure_muffling("under pivot, behind wall again", "rider behind the wall again - expect muffled speech", rider)

	rider.reparent(root)
	check_attached("after reparenting back under the root", root, false)
	await wait_raytraced_by_listener(rider)
	var restored := await measure_muffling("under root again", "rider back under the root - expect no change", rider)
	check_attached("after settling under the root again", root)

	# The rider loops, so it only stops if stop() releases it
	rider.stop()
	check(not VA.is_playing(rider), "looping rider still playing after stop()")
	await wait_frames(2)
	check(not VA.is_playing(rider), "looping rider playing again 2 frames after stop()")
	rider.play()
	check(VA.is_playing(rider), "rider not playing after play() following stop()")

	check(blocked.y < 0.5, "muffling HF %.4f behind the wall under the root, expected the partition to muffle it" % blocked.y)
	check(absf(reparented.y - blocked.y) < 0.05, "muffling HF %.4f after reparenting in place differs from %.4f before, expected the same position to sound the same" % [reparented.y, blocked.y])
	check(clear.y > 0.9, "muffling HF %.4f after moving the pivot to the listener's side, expected the rider to follow it out from behind the wall" % clear.y)
	check(back.y < clear.y * 0.5, "muffling HF %.4f after moving the pivot back isn't below half the clear gain %.4f" % [back.y, clear.y])
	check(absf(restored.y - blocked.y) < 0.05, "muffling HF %.4f after reparenting back under the root differs from %.4f at the start" % [restored.y, blocked.y])

# The emitter is recreated on re-entering the tree, so it isn't raytraced again until the next pass
func check_attached(label: String, parent: Node, raytraced := true) -> void:
	check(rider.get_parent() == parent, "rider's parent is %s %s, expected %s" % [rider.get_parent(), label, parent])
	check(VA.is_playing(rider), "rider stopped playing %s" % label)
	if raytraced:
		check(VA.is_raytraced(rider), "rider isn't raytraced %s" % label)

func slide(target, banner: String) -> void:
	if listen:
		print(">> %s" % banner)
	var tween := root.create_tween()
	tween.tween_property(pivot, "position", target, duration(1.0))
	await tween.finished

func teardown() -> void:
	if is_instance_valid(rider):
		rider.queue_free()
	pivot.queue_free()
	partition.queue_free()
	await wait_grouped_eax_count(baseline_eax)
