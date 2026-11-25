# AI-created

@tool
extends EditorExportPlugin

const VAOpenALLibrary = preload("VAOpenALLibrary.gd")

# .mhr files aren't Godot resources, so exports skip them unless they're added to the include filter by hand. This bundles the audio/vaudio/openal_hrtf_folder ones automatically. It also ships the export target's OpenAL Soft library beside the exported executable, where the OpenAL bindings load it from

func _get_name() -> String:
	return "VAHRTFExportPlugin"

func _addon_root() -> String:
	return get_script().resource_path.get_base_dir().get_base_dir().get_base_dir()

func _export_begin(features: PackedStringArray, is_debug: bool, path: String, flags: int) -> void:
	_export_openal(features)
	_export_hrtf()

func _export_openal(features: PackedStringArray) -> void:
	var os_name := VAOpenALLibrary.get_feature_os(features)
	var library_path := VAOpenALLibrary.get_library_path(_addon_root(), os_name, VAOpenALLibrary.get_feature_arch(features))

	if library_path.is_empty():
		push_warning("[vaudio] OpenAL Soft isn't available for this export platform (%s), so the exported game will have no audio" % ", ".join(features))
		return

	if not FileAccess.file_exists(library_path):
		push_error("[vaudio] %s is missing, so the exported game will have no audio. Copy OpenAL Soft into %s" % [library_path, ProjectSettings.globalize_path(library_path.get_base_dir())])
		return

	add_shared_object(library_path, PackedStringArray(), "")

func _export_hrtf() -> void:
	var folder: String = ProjectSettings.get_setting("audio/vaudio/openal_hrtf_folder", "res://hrtf")

	if folder.is_empty() or not DirAccess.dir_exists_absolute(folder):
		return

	for file in DirAccess.get_files_at(folder):
		if file.get_extension().to_lower() != "mhr":
			continue

		var file_path := folder.path_join(file)
		add_file(file_path, FileAccess.get_file_as_bytes(file_path), false)
