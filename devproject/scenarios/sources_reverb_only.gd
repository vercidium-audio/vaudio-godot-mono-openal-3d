extends "res://scenarios/lib/scenario.gd"

# Every source type's reverb_only turns off only its own dry output, and VAWorld.reverb_only turns off every source's, whichever is set. Sources added while the world is reverb-only start with their dry output off. Checked through is_dry_output_enabled(), the direct filter state last applied to the source's sounds. VAInputStreamSource isn't spawned, as it opens the microphone, and shares VAStreamSource's code

const SEED := 20261114
const AL_FORMAT_MONO16 := 4353

var spawned: Array[Node] = []
var sources: Array[Node] = []

func _init() -> void:
	scene = REVERB_ROOM

func run() -> void:
	var source := spawn_source("ReverbOnlySource", root.source.position + offset(-1), SEED, true, false)
	spawned.append(source)

	var stream := spawn_raytraced("VAStreamSource", "ReverbOnlyStream", -2, SEED + 1)
	var networked := spawn_raytraced("VANetworkedStreamSource", "ReverbOnlyNetworked", -3, SEED + 2)
	check(VA.open_stream(stream, AL_FORMAT_MONO16, 44100), "open_stream failed")

	var ambient := spawn_unraytraced("VASourceAmbient", "ReverbOnlyAmbient", root)
	var relative := spawn_unraytraced("VASourceRelative", "ReverbOnlyRelative", root)
	var leech := spawn_unraytraced("VASourceLeech", "ReverbOnlyLeech", source)

	sources = [source, stream, networked, ambient, relative, leech]

	for s in [source, stream, networked]:
		await wait_raytraced_by_listener(s)
	await wait_raytraced(SETTLE_PASSES)

	expect_dry("defaults", func(_i): return true)

	# Each source's own setting only affects that source
	for i in sources.size():
		VA.set_value(sources[i], "reverb_only", true)
		await wait_frames(2)
		expect_dry("only %s reverb-only" % sources[i].name, func(j): return j != i)

		VA.set_value(sources[i], "reverb_only", false)
		await wait_frames(2)
		expect_dry("%s reverb-only cleared" % sources[i].name, func(_j): return true)

	# The world's setting applies to every source, and a source's own setting can't turn its dry output back on
	VA.set_value(root.world, "reverb_only", true)
	VA.set_value(source, "reverb_only", true)
	await wait_frames(2)
	expect_dry("world reverb-only", func(_j): return false)

	VA.set_value(source, "reverb_only", false)
	await wait_frames(2)
	expect_dry("world reverb-only, source cleared", func(_j): return false)

	# A source added while the world is reverb-only starts without dry output
	var late := spawn_source("ReverbOnlyLate", root.source.position + offset(-4), SEED + 3, true, true)
	spawned.append(late)
	check(await wait_playing(late), "late source isn't playing")
	check(not VA.is_dry_output_enabled(late), "a source added while the world is reverb-only has dry output")
	await step("world reverb-only - expect only reverb", 1.0)

	VA.set_value(root.world, "reverb_only", false)
	await wait_frames(2)
	expect_dry("world reverb-only cleared", func(_j): return true)
	check(VA.is_dry_output_enabled(late), "the late source's dry output isn't back on")

	VA.set_value(late, "reverb_only", true)
	await step("late source reverb-only - expect only its reverb", 1.0)

func teardown() -> void:
	VA.set_value(root.world, "reverb_only", false)
	for node in spawned:
		if is_instance_valid(node):
			node.queue_free()

func expect_dry(when: String, expected: Callable) -> void:
	for i in sources.size():
		var want: bool = expected.call(i)
		check(VA.is_dry_output_enabled(sources[i]) == want, "%s: %s dry output enabled isn't %s" % [when, sources[i].name, want])

# Spread along the room's depth so the sources don't share one spot
func offset(index: int):
	var axis = Vector3(0, 0, 1) if root.source.position is Vector3 else Vector2(0, 1)
	return axis * (0.5 * index)

func spawn_raytraced(type: String, node_name: String, index: int, scattering_seed: int) -> Node:
	var node := VA.create_node(root.world, type)
	node.name = node_name
	node.position = root.source.position + offset(index)
	VA.set_value(node, "reverb_ray_count", 64)
	VA.set_value(node, "reverb_bounce_count", 64)
	VA.set_value(node, "scattering_seed", scattering_seed)
	root.add_child(node)
	spawned.append(node)
	return node

func spawn_unraytraced(type: String, node_name: String, parent: Node) -> Node:
	var node := VA.create_node(root.world, type)
	node.name = node_name
	VA.set_value(node, "streams", VA.get_value(root.source, "streams"))
	parent.add_child(node)
	spawned.append(node)
	return node
