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

static func set_value(node: Object, property: String, value: Variant) -> void:
	var name := csharp_name(property)
	if not name in node:
		push_error("[devproject] %s has no property '%s'" % [node, name])
		return
	node.set(name, value)

# Creates a plugin node by class name, e.g. "VADefaultMaterial". C# nodes aren't in ClassDB, so find the script next to the world's own script in the addon
static func create_node(world: Node, type: String) -> Node:
	var addon: String = world.get_script().resource_path.get_base_dir().get_base_dir()
	for folder in ["common/nodes", "nodes"]:
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
