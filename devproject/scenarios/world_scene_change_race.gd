extends "res://scenarios/lib/scenario.gd"

# Changes scene again straight after scene_changed, so the new scene's VAWorld leaves the tree before its deferred scene scan runs. The world must skip the scan and not disconnect tree signals it never connected

func run() -> void:
	var tree := root.get_tree()

	tree.change_scene_to_file(REVERB_ROOM)
	await tree.scene_changed
	tree.change_scene_to_file(TEST_SCENE)
	await tree.scene_changed

	# Let the removed world's deferred call flush, and the new world finish its own scan
	for i in 5:
		await tree.process_frame

	root = tree.current_scene
	check(root.scene_file_path == TEST_SCENE, "didn't end up back in %s" % TEST_SCENE)
	await wait_raytraced()
