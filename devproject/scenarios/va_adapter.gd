extends "res://scenarios/lib/scenario.gd"

# Calls every va.gd function so a renamed C# or native member fails here, rather than as a confusing failure in some other scenario

const EMITTER_PROPERTIES := ["reverb_ray_count", "reverb_bounce_count", "raytrace_once", "refresh_distance_threshold", "scattering_seed", "affects_grouped_eax", "ambient_occlusion_ray_count"]
const WORLD_PROPERTIES := ["humidity", "temperature", "pressure", "meters_per_unit", "maximum_grouped_eax_count", "master_volume", "reverb_only"]

var original_ray_count: int

func setup() -> void:
	original_ray_count = VA.get_value(root.source, "reverb_ray_count")

func run() -> void:
	for property in EMITTER_PROPERTIES:
		check(VA.get_value(root.source, property) != null, "source property %s is null" % property)
	for property in WORLD_PROPERTIES:
		check(VA.get_value(root.world, property) != null, "world property %s is null" % property)

	var debug_window := OS.get_cmdline_user_args().has("--debugwindow")
	check(VA.get_value(root.world, "rendering_enabled") == debug_window, "rendering_enabled should be %s when --debugwindow is %s" % [debug_window, "passed" if debug_window else "not passed"])
	if OS.get_cmdline_user_args().has("--mute"):
		check(VA.get_value(root.world, "master_volume") == 0.0, "master_volume should be 0 when --mute is passed")

	await wait_raytraced_by_listener(root.source)
	check(VA.is_raytraced(root.source), "raytraced by listener but not raytraced")

	var count := VA.raytrace_count(root.world)
	await wait_raytraced()
	check(VA.raytrace_count(root.world) >= count + 2, "raytrace count didn't advance")

	var lf := VA.muffling_lf(root.source)
	var hf := VA.muffling_hf(root.source)
	check(lf >= 0.0 and lf <= 1.0, "muffling LF %f out of range" % lf)
	check(hf >= 0.0 and hf <= 1.0, "muffling HF %f out of range" % hf)
	check(VA.raytracing_time(root.world) >= 0.0, "negative raytracing time")

	var index := VA.grouped_eax_index(root.source)
	var groups := VA.grouped_eax_count(root.world)
	check(index < groups, "grouped EAX index %d >= count %d" % [index, groups])

	if index >= 0:
		check(VA.grouped_eax_decay_time(root.world, index) >= 0.0, "negative decay time")
		check(VA.grouped_eax_gain_lf(root.world, index) >= 0.0, "negative reverb gain LF")
		check(VA.grouped_eax_gain_hf(root.world, index) >= 0.0, "negative reverb gain HF")
		check(VA.decay_time(root.world, root.source) == VA.grouped_eax_decay_time(root.world, index), "decay_time doesn't match the source's group")

	# Already current, so this is a no-op
	VA.make_current(root.listener)
	check(VA.get_value(root.listener, "current") == true, "scene listener isn't current after make_current()")

	VA.sync_primitive(root.world, root.listener)
	await wait_raytraced()
	check(VA.is_raytraced(root.source), "not raytraced after sync_primitive")

	VA.set_value(root.source, "reverb_ray_count", 128)
	check(VA.get_value(root.source, "reverb_ray_count") == 128, "reverb_ray_count didn't round-trip")
	await wait_raytraced()
	check(VA.is_raytraced(root.source), "not raytraced after changing reverb_ray_count")

	print("[devproject] raytrace count %d, muffling LF %.3f HF %.3f, grouped EAX %d/%d, decay time %.3f" % [VA.raytrace_count(root.world), lf, hf, index, groups, VA.decay_time(root.world, root.source)])

func teardown() -> void:
	VA.set_value(root.source, "reverb_ray_count", original_ray_count)
