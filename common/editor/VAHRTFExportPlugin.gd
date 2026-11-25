# AI-created

@tool
extends EditorExportPlugin

const VAOpenALLibrary = preload("VAOpenALLibrary.gd")
const VAProjectSettings = preload("VAProjectSettings.gd")

# Ships the selected audio backend's library beside the exported executable, where the backend loads it from. OpenAL: the addon's OpenAL Soft, plus the audio/vaudio/openal_hrtf_folder .mhr files (they aren't Godot resources, so exports skip them unless they're added to the include filter by hand). FMOD: the user's own FMOD library, since vaudio doesn't ship FMOD

func _get_name() -> String:
	return "VAHRTFExportPlugin"

func _addon_root() -> String:
	return get_script().resource_path.get_base_dir().get_base_dir().get_base_dir()

func _export_begin(features: PackedStringArray, is_debug: bool, path: String, flags: int) -> void:
	match VAProjectSettings.selected_backend():
		"OpenAL":
			_export_openal(features)
			_export_hrtf()
		"FMOD":
			_export_fmod(features)

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

# The names FMODLibrary.cs loads at runtime
static func _fmod_library_name(os_name: String) -> String:
	match os_name:
		"windows":
			return "fmod.dll"
		"linux":
			return "libfmod.so"
		"macos":
			return "libfmod.dylib"

	return ""

# Looks in the project folder, then audio/vaudio/fmod_library_path (the library or its folder). The library must be the export target's platform and architecture
func _export_fmod(features: PackedStringArray) -> void:
	var library_name := _fmod_library_name(VAOpenALLibrary.get_feature_os(features))

	if library_name.is_empty():
		push_warning("[vaudio] FMOD isn't supported on this export platform (%s), so the exported game will have no audio" % ", ".join(features))
		return

	var candidates := PackedStringArray([ProjectSettings.globalize_path("res://" + library_name)])

	var setting: String = ProjectSettings.get_setting("audio/vaudio/fmod_library_path", "")
	if not setting.is_empty():
		var path := ProjectSettings.globalize_path(setting)
		candidates.append(path.path_join(library_name) if DirAccess.dir_exists_absolute(path) else path)

	for candidate in candidates:
		if FileAccess.file_exists(candidate):
			add_shared_object(candidate, PackedStringArray(), "")
			return

	push_warning("[vaudio] The FMOD library %s wasn't found, so it isn't in the export. Copy it from the FMOD Engine (FMOD Studio API) 2.03.x install beside the exported game's executable, or the game will have no audio. Searched: %s" % [library_name, ", ".join(candidates)])
