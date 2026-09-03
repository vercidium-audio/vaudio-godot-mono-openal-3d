@tool
extends EditorDebuggerPlugin

func sync_primitive(scene_root_name: String, node_path: NodePath, material: String, use_flat_transmission,
		propagate: String = "", propagate_layer = null) -> void:
	for session in get_sessions():
		# Only sessions currently attached to a running game can receive messages - a session
		# stays in get_sessions() after the game it was attached to has stopped.
		if session != null and session.is_active():
			session.send_message("vaudio:sync_primitive", [scene_root_name, node_path, material,
				use_flat_transmission, propagate, propagate_layer])

func sync_material_properties(scene_root_name: String, node_path: NodePath, node_name: String,
		is_custom_material: bool, material_type: int, custom_material_name: String,
		absorption_lf: float, absorption_hf: float, scattering: float, transmission_lf: float,
		transmission_hf: float, flat_transmission_lf: float, flat_transmission_hf: float,
		debug_color: Color) -> void:
	for session in get_sessions():
		if session != null and session.is_active():
			session.send_message("vaudio:sync_material_properties", [scene_root_name, node_path,
				node_name, is_custom_material, material_type, custom_material_name, absorption_lf,
				absorption_hf, scattering, transmission_lf, transmission_hf, flat_transmission_lf,
				flat_transmission_hf, debug_color])

func sync_viewport_camera(a, b, c) -> void:
	for session in get_sessions():
		if session != null and session.is_active():
			session.send_message("vaudio:sync_viewport_camera", [a, b, c])
