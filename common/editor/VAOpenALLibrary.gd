# AI-created

@tool
extends RefCounted

# OpenAL Soft lives in the addon's bin/{folder}/, named like the Vercidium Audio SDK package's platform folders (windows, windows-arm64, linux, linux-arm64, mac)

# os_name is lowercase like the export feature tags (windows, linux, macos). Returns "" for unsupported platforms
static func get_folder(os_name: String, arch: String) -> String:
	if os_name == "macos":
		return "mac"

	if os_name != "windows" and os_name != "linux":
		return ""

	match arch:
		"x86_64":
			return os_name
		"arm64":
			return os_name + "-arm64"

	return ""

static func get_library_name(os_name: String) -> String:
	match os_name:
		"windows":
			return "soft_oal.dll"
		"linux":
			return "libopenal.so.1"
		"macos":
			return "libopenal.1.dylib"

	return ""

# The export target's platform, from its feature tags
static func get_feature_os(features: PackedStringArray) -> String:
	for os_name in ["windows", "linux", "macos"]:
		if features.has(os_name):
			return os_name

	return ""

static func get_feature_arch(features: PackedStringArray) -> String:
	for arch in ["x86_64", "arm64"]:
		if features.has(arch):
			return arch

	return ""

# e.g. res://addons/vaudio-godot-mono-openal-3d/bin/windows-arm64/soft_oal.dll, or "" for unsupported platforms
static func get_library_path(addon_root: String, os_name: String, arch: String) -> String:
	var folder := get_folder(os_name, arch)
	var library := get_library_name(os_name)

	if folder.is_empty() or library.is_empty():
		return ""

	return "%s/bin/%s/%s" % [addon_root, folder, library]
