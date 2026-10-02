extends Logger

# Captures every engine/plugin error and warning (push_error/push_warning, C# GD.PushError/PushWarning, native VA_ERROR/VA_WARN) so the runner can check what each scenario produced. Installed by runner.gd via OS.add_logger - loggers can be called from any thread

var errors: PackedStringArray = []
var warnings: PackedStringArray = []
var mutex := Mutex.new()

func _log_error(_function: String, _file: String, _line: int, code: String, rationale: String, _editor_notify: bool, error_type: int, _script_backtraces: Array[ScriptBacktrace]) -> void:
	var text := code if rationale.is_empty() else "%s %s" % [code, rationale]
	mutex.lock()
	if error_type == ERROR_TYPE_WARNING:
		warnings.append(text)
	else:
		errors.append(text)
	mutex.unlock()

# Returns {errors, warnings} logged since the last call, and clears them
func take() -> Dictionary:
	mutex.lock()
	var taken := {"errors": errors, "warnings": warnings}
	errors = []
	warnings = []
	mutex.unlock()
	return taken
