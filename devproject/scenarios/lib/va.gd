# AI-created

extends RefCounted

# C# (Mono) plugin adapter - the Standard plugin has its own va.gd with the same functions. Property names are always given in snake_case and converted to the C# PascalCase name here.

# to_pascal_case() gets acronyms wrong (GroupedEax, GainHf), so fix up the ones the C# plugin uses
const ACRONYMS := {"Eax": "EAX", "Lf": "LF", "Hf": "HF"}

# Properties whose C# name isn't just the PascalCase native name
const RENAMES := {"bounds_size": "Size"}

static func csharp_name(property: String) -> String:
	if RENAMES.has(property):
		return RENAMES[property]
	var words := property.split("_", false)
	var result := ""
	for word in words:
		var pascal := word.capitalize()
		result += ACRONYMS.get(pascal, pascal)
	return result

static func get_value(node: Object, property: String) -> Variant:
	var name := csharp_name(property)
	if not name in node:
		push_error("[devproject] %s has no property '%s'" % [node, name])
		return null
	return node.get(name)

# The property's info from get_property_list(), after the node's _ValidateProperty, or empty if it has none
static func property_info(node: Object, property: String) -> Dictionary:
	var name := csharp_name(property)
	for info in node.get_property_list():
		if info.name == name:
			return info
	return {}

# Whether the property is shown in the inspector, after the node's _ValidateProperty
static func is_in_inspector(node: Object, property: String) -> bool:
	return property_info(node, property).get("usage", 0) & PROPERTY_USAGE_EDITOR != 0

static func set_value(node: Object, property: String, value: Variant) -> void:
	var name := csharp_name(property)
	if not name in node:
		push_error("[devproject] %s has no property '%s'" % [node, name])
		return
	node.set(name, value)

static func connect_signal(node: Object, signal_name: String, callable: Callable) -> void:
	var name := csharp_name(signal_name)
	if not node.has_signal(name):
		push_error("[devproject] %s has no signal '%s'" % [node, name])
		return
	node.connect(name, callable)

# Creates a plugin node by class name, e.g. "VADefaultMaterial". C# nodes aren't in ClassDB, so find the script next to the world's own script in the addon
static func create_node(world: Node, type: String) -> Node:
	var addon: String = world.get_script().resource_path.get_base_dir().get_base_dir()
	for folder in ["common/nodes", "nodes", "common/player", "player"]:
		var path := "%s/%s/%s.cs" % [addon, folder, type]
		if ResourceLoader.exists(path):
			return load(path).new()
	push_error("[devproject] no C# script for %s in %s" % [type, addon])
	return null

static func is_raytraced(source: Node) -> bool:
	return source.Raytraced

static func is_raytraced_by_listener(source: Node) -> bool:
	return source.IsRaytracedByListener

static func muffling_lf(source: Node) -> float:
	return source.GainLF

static func muffling_hf(source: Node) -> float:
	return source.GainHF

static func grouped_eax_index(source: Node) -> int:
	return source.GroupedEAXIndex

static func raytrace_count(world: Node) -> int:
	return world.GetRaytraceCount()

static func bounds_position(world: Node) -> Variant:
	return world.GetBoundsPosition()

static func is_ready_to_play(source: Node) -> bool:
	return source.IsReadyToPlay

static func is_within_bounds(world: Node, position) -> bool:
	return world.IsWithinBounds(position)

static func is_silenced_by_bounds(source: Node) -> bool:
	return source.IsSilencedByBounds()

# Whether the source's sound is sent to a reverb effect (grouped or the listener's)
static func has_reverb(source: Node) -> bool:
	return source.HasReverb()

static func is_dry_output_enabled(source: Node) -> bool:
	return source.IsDryOutputEnabled()

static func raytracing_time(world: Node) -> float:
	return world.GetRaytracingTime()

static func grouped_eax_count(world: Node) -> int:
	return world.GetGroupedEAXCount()

static func grouped_eax_decay_time(world: Node, index: int) -> float:
	return world.GetGroupedEAXDecayTime(index)

static func grouped_eax_gain_lf(world: Node, index: int) -> float:
	return world.GetGroupedEAXGainLF(index)

static func grouped_eax_gain_hf(world: Node, index: int) -> float:
	return world.GetGroupedEAXGainHF(index)

static func export_to_file(world: Node, path: String) -> bool:
	return world.ExportToFile(path)

static func sync_primitive(world: Node, node: Node) -> void:
	world.SyncPrimitive(node)

# Decay time of the grouped reverb this source contributes to, or -1 if it isn't grouped yet
static func decay_time(world: Node, source: Node) -> float:
	var index := grouped_eax_index(source)
	if index < 0 or index >= grouped_eax_count(world):
		return -1.0
	return grouped_eax_decay_time(world, index)

static func is_playing(source: Node) -> bool:
	return source.IsPlaying()

static func make_current(listener: Node) -> void:
	listener.MakeCurrent()

# VAPcmFormat values, for open_stream(), open_capture() and VAInputStreamSource's format
const PCM_MONO16 := 1
const PCM_MONO_FLOAT := 4

static func open_stream(source: Node, format: int, frequency: int) -> bool:
	return source.OpenStream(format, frequency)

static func push_audio_data(source: Node, data: PackedByteArray) -> void:
	source.PushAudioData(data)

static func open_capture(source: Node, device_name: String, format: int, frequency: int, buffer_size_frames: int) -> bool:
	return source.OpenCapture(device_name, format, frequency, buffer_size_frames)

static func is_capture_open(source: Node) -> bool:
	return source.IsCaptureOpen

static func close_stream(source: Node) -> void:
	source.CloseStream()

static func is_stream_open(source: Node) -> bool:
	return source.IsStreamOpen

static func play(source: Node) -> bool:
	return source.Play()

static func stop(source: Node) -> void:
	source.Stop()

static func play_from(source: Node, start_time: float) -> bool:
	return source.Play(start_time)

static func set_stream_paused(source: Node, paused: bool) -> void:
	source.StreamPaused = paused

static func is_stream_paused(source: Node) -> bool:
	return source.StreamPaused

static func seek(source: Node, time: float) -> void:
	source.Seek(time)

static func playback_time(source: Node) -> float:
	return source.GetPlaybackTime()

# The C# VAAudioBackend isn't a Godot object, so its HRTF functions are reached through VAWorld's static passthroughs
static func available_hrtfs(world: Node) -> PackedStringArray:
	return PackedStringArray(world.get_script().GetAvailableHrtfs())

static func set_hrtf(world: Node, hrtf_name: String) -> bool:
	return world.get_script().SetHrtf(hrtf_name)

static func get_hrtf(world: Node) -> String:
	return world.get_script().GetHrtf()

static func active_hrtf(world: Node) -> String:
	return world.get_script().GetActiveHrtf()

static func set_hrtf_enabled(world: Node, value: bool) -> bool:
	return world.get_script().SetHrtfEnabled(value)

# The global mix settings the audio backend is using, keyed by the VAWorld property they come from in snake_case, e.g. "doppler_scale"
static func backend_settings(world: Node) -> Dictionary:
	var settings := {}
	var pascal: Dictionary = world.get_script().GetBackendSettings()
	for key in pascal:
		settings[String(key).to_snake_case()] = pascal[key]
	return settings

# How many errors the audio backend has logged since the game started, through VAWorld's static passthrough like the HRTF ones
static func backend_error_count(world: Node) -> int:
	return world.get_script().GetBackendErrorCount()

# The audio backend in use, e.g. "OpenAL" or "FMOD"
static func backend_name(world: Node) -> String:
	return world.get_script().GetBackendName()

static func is_backend_initialised(world: Node) -> bool:
	return world.get_script().IsBackendInitialised()
