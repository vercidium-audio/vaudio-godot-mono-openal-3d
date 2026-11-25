# AI-created

@tool
extends RefCounted

# Keeps the vaudio parts of the user's .csproj up to date. The backend block makes MSBuild read audio/vaudio/backend from project.godot, so only the selected backend's folder is compiled and only OpenAL pulls in the openal_soft_bindings package. Switching backend just needs a rebuild, the .csproj itself doesn't change

const BACKEND_BEGIN = "<!-- vaudio audio backend (managed by the vaudio plugin) -->"
const BACKEND_END = "<!-- end vaudio audio backend -->"

const OPENAL_BINDINGS_VERSION = "1.0.10"

const PROPERTY_GROUP = """    <PropertyGroup>
        <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
        <NoWarn>$(NoWarn);1591</NoWarn>
    </PropertyGroup>"""

static func _reference(addon_name: String) -> String:
	return """    <ItemGroup>
        <Reference Include="vaudio">
            <HintPath>addons\\%s\\bin\\vaudio.dll</HintPath>
        </Reference>
    </ItemGroup>""" % addon_name

# Each backend's folder is removed from compilation unless it's the selected one. The regex reads vaudio/backend from project.godot's [audio] section, defaulting to OpenAL
static func _backend_block(addon_name: String) -> String:
	return """    %s
    <PropertyGroup>
        <VaudioAddonDir>addons\\%s</VaudioAddonDir>
        <VaudioBackend Condition="Exists('$(MSBuildProjectDirectory)\\project.godot')">$([System.Text.RegularExpressions.Regex]::Match($([System.IO.File]::ReadAllText('$(MSBuildProjectDirectory)\\project.godot')), '(?m)(?&lt;=^\\[audio\\][^\\[]*^vaudio/backend=")[^"]*').Value)</VaudioBackend>
        <VaudioBackend Condition="'$(VaudioBackend)' == ''">OpenAL</VaudioBackend>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="openal_soft_bindings" Version="%s" Condition="'$(VaudioBackend)' == 'OpenAL'" />
        <Compile Remove="$(VaudioAddonDir)\\common\\backend\\openal\\**" Condition="'$(VaudioBackend)' != 'OpenAL'" />
        <Compile Remove="$(VaudioAddonDir)\\common\\backend\\fmod\\**" Condition="'$(VaudioBackend)' != 'FMOD'" />
    </ItemGroup>
    %s""" % [BACKEND_BEGIN, addon_name, OPENAL_BINDINGS_VERSION, BACKEND_END]

# Returns the updated .csproj text, or the same text when it's already up to date
static func update(content: String, addon_name: String) -> String:
	if "vaudio.dll" not in content:
		content = _insert(content, _reference(addon_name))

	if "AllowUnsafeBlocks" not in content:
		content = _insert(content, PROPERTY_GROUP)

	var block := _backend_block(addon_name)
	var begin := content.find(BACKEND_BEGIN)
	var end := content.find(BACKEND_END)

	if begin != -1 and end > begin:
		# Replace an older version of the block, including its indentation
		var line_start := content.rfind("\n", begin) + 1
		return content.substr(0, line_start) + block + content.substr(end + BACKEND_END.length())

	# 1.11 projects referenced openal_soft_bindings unconditionally, which the block now does for OpenAL only
	content = RegEx.create_from_string("[ \\t]*<PackageReference Include=\"openal_soft_bindings\"[^>]*/>[ \\t]*\\r?\\n?").sub(content, "", true)
	content = RegEx.create_from_string("[ \\t]*<ItemGroup>\\s*</ItemGroup>[ \\t]*\\r?\\n?").sub(content, "", true)
	content = RegEx.create_from_string("(\\r?\\n)(?:[ \\t]*\\r?\\n){2,}").sub(content, "$1$1", true)

	return _insert(content, block)

static func _insert(content: String, text: String) -> String:
	var pos := content.rfind("</Project>")
	if pos == -1:
		return content
	var before := content.substr(0, pos).rstrip(" \t\r\n")
	return before + "\n\n" + text + "\n" + content.substr(pos)
