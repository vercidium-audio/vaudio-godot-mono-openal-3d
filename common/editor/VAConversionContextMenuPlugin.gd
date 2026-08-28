@tool
extends EditorContextMenuPlugin

# Right-click to convert AudioStreamPlayer3D to VASource / VASourceRelative / VASourceLeech / VASourceAmbient,
# or AudioStreamPlayer to VASourceRelative / VASourceAmbient. Mirrors the conversion offered by the native
# (C++ GDExtension) plugin's ConversionContextMenuPlugin, adapted to this addon's node/property names.
#
# Shared verbatim between the 2D and 3D addons via each addon's `common` symlink - every vaudio type
# referenced here is [GlobalClass] so it resolves by name without an addon-specific preload().

func _popup_menu(paths: PackedStringArray) -> void:
	if paths.is_empty():
		return

	var scene_root = EditorInterface.get_edited_scene_root()
	if not scene_root:
		return

	var any_eligible = false
	var all_not_va_source = true
	var all_not_va_source_relative = true
	var all_not_va_source_leech_and_parent_is_emitter = true
	var all_not_va_source_ambient = true

	for path in paths:
		var node = scene_root.get_node_or_null(NodePath(path))
		if not node:
			return

		# The spatialised built-in player - AudioStreamPlayer3D in a 3D project, AudioStreamPlayer2D
		# in a 2D one. This file is shared by both addons; a project only ever contains one of them.
		var is_audio_player_3d = node.get_class() == "AudioStreamPlayer3D" or node.get_class() == "AudioStreamPlayer2D"
		var is_audio_player = node.get_class() == "AudioStreamPlayer"
		var is_va_source = node is VASource
		var is_va_source_relative = node is VASourceRelative
		var is_va_source_leech = node is VASourceLeech
		var is_va_source_ambient = node is VASourceAmbient

		if not (is_audio_player_3d or is_audio_player or is_va_source or is_va_source_relative or is_va_source_leech or is_va_source_ambient):
			return

		any_eligible = true

		# AudioStreamPlayer isn't spatialised, so it can only become a VASourceRelative/VASourceAmbient - never offer VASource/VASourceLeech for it.
		if is_audio_player:
			all_not_va_source = false
			all_not_va_source_leech_and_parent_is_emitter = false

		if is_va_source:
			all_not_va_source = false

		if is_va_source_relative:
			all_not_va_source_relative = false

		if is_va_source_ambient:
			all_not_va_source_ambient = false

		# VASourceLeech must be a direct child of a VAEmitter to function (it leeches
		# that parent's raytracing results instead of casting its own) - only offer
		# the conversion when that's actually every node's parent.
		var parent_is_va_emitter = node.get_parent() is VAEmitter

		if is_va_source_leech or not parent_is_va_emitter:
			all_not_va_source_leech_and_parent_is_emitter = false

	if not any_eligible:
		return

	if all_not_va_source:
		add_context_menu_item("Convert to VASource", convert_selected_to.bind("VASource"))

	if all_not_va_source_relative:
		add_context_menu_item("Convert to VASourceRelative", convert_selected_to.bind("VASourceRelative"))

	if all_not_va_source_leech_and_parent_is_emitter:
		add_context_menu_item("Convert to VASourceLeech", convert_selected_to.bind("VASourceLeech"))

	if all_not_va_source_ambient:
		add_context_menu_item("Convert to VASourceAmbient", convert_selected_to.bind("VASourceAmbient"))

func convert_selected_to(nodes: Array, target_class: String) -> void:
	for node in nodes:
		convert_node(node, target_class)

func convert_node(old_node: Node, target_class: String) -> void:
	if not old_node:
		return

	var parent = old_node.get_parent()
	if not parent:
		push_warning("[vaudio] Cannot convert the scene root node.")
		return

	var new_node
	match target_class:
		"VASource":
			new_node = VASource.new()
		"VASourceRelative":
			new_node = VASourceRelative.new()
		"VASourceLeech":
			new_node = VASourceLeech.new()
		"VASourceAmbient":
			new_node = VASourceAmbient.new()
		_:
			return

	var old_name = old_node.name
	var old_index = old_node.get_index()
	var owner = old_node.owner

	# Convert AudioStreamPlayer(3D)'s logarithmic volume_db to this addon's linear Volume
	if has_property(old_node, "volume_db"):
		new_node.Volume = db_to_linear(old_node.volume_db)

	copy_property(old_node, new_node, "pitch_scale", "Pitch")

	# AudioStreamPlayer3D has a single `stream` resource - this addon's nodes instead take a pool
	# of streams to pick randomly from via the `Streams` array, so wrap it as a one-entry array.
	# AudioStreamRandomizer picks a random sub-stream at playback time - expand its sub-streams
	# into `Streams` directly so an existing scene using it keeps the same pool of sounds (minus
	# its own pitch/volume randomisation, which this addon's nodes don't have an equivalent for).
	if has_property(old_node, "stream") and old_node.stream != null:
		var old_stream = old_node.stream

		if old_stream is AudioStreamRandomizer:
			var extracted_streams = []

			for i in range(old_stream.get_streams_count()):
				var sub_stream = old_stream.get_stream(i)

				if sub_stream != null:
					extracted_streams.append(sub_stream)

			new_node.Streams = extracted_streams
		else:
			new_node.Streams = [old_stream]

	var is_spatialised_target = target_class == "VASource" or target_class == "VASourceLeech"

	if is_spatialised_target:
		copy_property(old_node, new_node, "max_distance", "MaxDistance")
		copy_property(old_node, new_node, "unit_size", "ReferenceDistance")

	if target_class != old_node.get_class():
		copy_property(old_node, new_node, "Volume", "Volume")
		copy_property(old_node, new_node, "Pitch", "Pitch")
		copy_property(old_node, new_node, "Looping", "Looping")
		copy_property(old_node, new_node, "Streams", "Streams")
		copy_property(old_node, new_node, "PlaybackNoRepeat", "PlaybackNoRepeat")

		if is_spatialised_target:
			copy_property(old_node, new_node, "MaxDistance", "MaxDistance")
			copy_property(old_node, new_node, "ReferenceDistance", "ReferenceDistance")

	# AudioStreamPlayer(3D) has no Looping property of its own - Godot instead loops based on the imported .wav's
	# "Loop Mode" (Import tab). Derive Looping from that when converting from one of those. Only applies when
	# old_node didn't already have its own Looping property (i.e. wasn't itself one of this addon's sources) -
	# that value already won above via copy_property(old_node, new_node, "Looping", "Looping").
	if not has_property(old_node, "Looping"):
		new_node.Looping = any_stream_wants_looping(new_node.Streams)

	# Carry the spatial transform across (Node2D or Node3D - the 2D and 3D addons share this file).
	if old_node is Node3D and new_node is Node3D:
		new_node.transform = old_node.transform
	elif old_node is Node2D and new_node is Node2D:
		new_node.transform = old_node.transform

	# Move children across before the old node is freed
	while old_node.get_child_count() > 0:
		var child = old_node.get_child(0)
		old_node.remove_child(child)
		new_node.add_child(child)
		child.owner = owner

	parent.remove_child(old_node)
	old_node.queue_free()

	new_node.name = old_name
	parent.add_child(new_node)
	parent.move_child(new_node, old_index)
	new_node.owner = owner

	EditorInterface.get_selection().clear()
	EditorInterface.get_selection().add_node(new_node)
	EditorInterface.mark_scene_as_unsaved()

# AudioStreamPlayer3D is a built-in class - property existence has to be checked at runtime via get_property_list
func has_property(object: Object, property_name: String) -> bool:
	for info in object.get_property_list():
		if info["name"] == property_name:
			return true

	return false

func copy_property(source: Object, target: Object, from: String, to: String) -> bool:
	if not has_property(source, from):
		return false

	target.set(to, source.get(from))
	return true

# AudioStreamWAV's imported "Loop Mode" and AudioStreamOggVorbis's "Loop" checkbox are the closest equivalent to a
# Looping flag for a plain AudioStreamPlayer(3D). Ping pong / backwards aren't representable by this addon's simple
# looping flag, so any non-disabled WAV loop mode, or an enabled Ogg loop flag, counts.
func any_stream_wants_looping(streams: Array) -> bool:
	for stream in streams:
		if stream is AudioStreamWAV and stream.loop_mode != AudioStreamWAV.LOOP_DISABLED:
			return true

		if stream is AudioStreamOggVorbis and stream.loop:
			return true

	return false
