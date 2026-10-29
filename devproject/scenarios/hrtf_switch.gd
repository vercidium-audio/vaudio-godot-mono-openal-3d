extends "res://scenarios/lib/scenario.gd"

# Switches between every available HRTF (OpenAL Soft's built-in one, plus any .mhr files in audio/vaudio/hrtf_folder) while a source plays, checking the device resets in place without stopping it. Then checks an unknown HRTF name falls back to the default, and HRTF can be turned off and back on

const BUILT_IN_HRTF := "Built-In HRTF"

func run() -> void:
	await wait_raytraced_by_listener(root.source)
	check(await wait_playing(root.source), "source isn't playing before switching HRTF")

	var hrtfs := VA.available_hrtfs(root.world)
	print("[hrtf_switch] available HRTFs: ", hrtfs)
	check(hrtfs.has(BUILT_IN_HRTF), "'%s' isn't in the available HRTFs: %s" % [BUILT_IN_HRTF, hrtfs])
	check(VA.active_hrtf(root.world) != "", "HRTF isn't active at startup, with audio/vaudio/hrtf_enabled on")

	for hrtf in hrtfs:
		check(VA.set_hrtf(root.world, hrtf), "set_hrtf('%s') failed" % hrtf)
		check(VA.get_hrtf(root.world) == hrtf, "get_hrtf() is '%s' after set_hrtf('%s')" % [VA.get_hrtf(root.world), hrtf])
		check(VA.active_hrtf(root.world) == hrtf, "active HRTF is '%s' after set_hrtf('%s')" % [VA.active_hrtf(root.world), hrtf])
		await step("source playing with HRTF '%s'" % hrtf, 1.0)
		check(VA.is_playing(root.source), "source stopped playing after switching to HRTF '%s'" % hrtf)

	expect_warning("was not found, using the default HRTF instead")
	check(VA.set_hrtf(root.world, "Missing HRTF"), "set_hrtf() with an unknown name failed")
	check(VA.active_hrtf(root.world) == hrtfs[0], "active HRTF is '%s' after an unknown name, expected the default '%s'" % [VA.active_hrtf(root.world), hrtfs[0]])

	check(VA.set_hrtf(root.world, ""), "set_hrtf('') failed")
	check(VA.get_hrtf(root.world) == "", "get_hrtf() isn't empty after set_hrtf('')")

	check(VA.set_hrtf_enabled(root.world, false), "set_hrtf_enabled(false) failed")
	check(VA.active_hrtf(root.world) == "", "HRTF is still active ('%s') after set_hrtf_enabled(false)" % VA.active_hrtf(root.world))
	await step("source playing with HRTF off", 1.0)
	check(VA.is_playing(root.source), "source stopped playing after turning HRTF off")

	check(VA.set_hrtf_enabled(root.world, true), "set_hrtf_enabled(true) failed")
	check(VA.active_hrtf(root.world) != "", "HRTF isn't active after set_hrtf_enabled(true)")
	check(VA.is_playing(root.source), "source stopped playing after turning HRTF back on")

func teardown() -> void:
	VA.set_hrtf(root.world, "")
	VA.set_hrtf_enabled(root.world, true)
