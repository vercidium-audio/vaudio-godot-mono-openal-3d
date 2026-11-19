extends "res://scenarios/lib/scenario.gd"

# Scenes saved by older plugin versions reference each node's C# script by path (the per-user .uid files aren't shipped), so moving a node's file breaks every scene that uses it. Loads a scene referencing each path a released version bound a node to, and checks the node comes back with its class. If a node's body moves to common/, leave a per-addon file named after the class at the old path, like nodes/VASourceAmbient.cs

# Relative to the addon folder. {dim} is 2D or 3D
const LEGACY_PATHS := [
	"common/nodes/VAEmitter.cs",
	"common/nodes/VAListener.cs",
	"common/openal/nodes/ALSource.cs",
	"openal/nodes/ALSource{dim}.cs",
	"common/nodes/VAStreamSource.cs",
	"common/nodes/VAInputStreamSource.cs",
	"common/nodes/VANetworkedStreamSource.cs",
	"common/nodes/VASource.cs",
	"common/nodes/VASourceLeech.cs",
	"openal/nodes/ALSourceRelative.cs",
	"nodes/VASourceRelative.cs",
	"nodes/VASourceAmbient.cs",
	"nodes/VAVisualisation.cs",
	"world/VAWorld.cs",
	"common/nodes/VADefaultMaterial.cs",
	"common/nodes/VACustomMaterial.cs",
]

const SCENE_PATH := "user://scripts_legacy_paths.tscn"

func run() -> void:
	var addon: String = root.world.get_script().resource_path.get_base_dir().get_base_dir()
	var dim := "3D" if addon.ends_with("-3d") else "2D"

	for relative in LEGACY_PATHS:
		check_path("%s/%s" % [addon, relative.replace("{dim}", dim)])

func teardown() -> void:
	DirAccess.remove_absolute(SCENE_PATH)

func check_path(path: String) -> void:
	var class_name_expected := path.get_file().get_basename()
	if not ResourceLoader.exists(path):
		fail("%s no longer exists, so scenes saved by older versions lose their %s script" % [path, class_name_expected])
		return

	# A file that isn't named after its class (or only holds part of a partial class) isn't the one Godot binds, so it has no base type
	var script: Script = load(path)
	var base_type := script.get_instance_base_type()
	if base_type.is_empty():
		fail("%s isn't the file Godot binds %s to" % [path, class_name_expected])
		return

	var file := FileAccess.open(SCENE_PATH, FileAccess.WRITE)
	file.store_string("[gd_scene load_steps=2 format=3]\n\n[ext_resource type=\"Script\" path=\"%s\" id=\"1\"]\n\n[node name=\"%s\" type=\"%s\"]\nscript = ExtResource(\"1\")\n" % [path, class_name_expected, base_type])
	file.close()

	var scene: PackedScene = ResourceLoader.load(SCENE_PATH, "", ResourceLoader.CACHE_MODE_IGNORE)
	if scene == null:
		fail("a scene referencing %s didn't load" % path)
		return

	var node := scene.instantiate()
	var attached: Script = node.get_script()
	check(attached != null and attached.resource_path == path, "a scene referencing %s didn't attach the script" % path)
	node.free()
