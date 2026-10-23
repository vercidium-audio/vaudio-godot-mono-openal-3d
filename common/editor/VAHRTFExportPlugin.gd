@tool
extends EditorExportPlugin

# .mhr files aren't Godot resources, so exports skip them unless they're added to the include filter by hand. This bundles the audio/vaudio/hrtf_folder ones automatically

func _get_name() -> String:
	return "VAHRTFExportPlugin"

func _export_begin(features: PackedStringArray, is_debug: bool, path: String, flags: int) -> void:
	var folder: String = ProjectSettings.get_setting("audio/vaudio/hrtf_folder", "res://hrtf")

	if folder.is_empty() or not DirAccess.dir_exists_absolute(folder):
		return

	for file in DirAccess.get_files_at(folder):
		if file.get_extension().to_lower() != "mhr":
			continue

		var file_path := folder.path_join(file)
		add_file(file_path, FileAccess.get_file_as_bytes(file_path), false)
