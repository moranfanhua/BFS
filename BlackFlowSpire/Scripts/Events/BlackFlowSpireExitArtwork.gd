extends Control

@export var auto_play := true
var _data: Dictionary
var _time := 0.0
var _ambient_time := 0.0
var _materials: Array[ShaderMaterial] = []
const DURATION := 4.65

func _ready() -> void:
	_data = JSON.parse_string(FileAccess.get_file_as_string("res://BlackFlowSpire/Data/Events/exit-entry.json"))
	_collect_materials(self)
	seek_entry(0.0 if auto_play else DURATION)
	set_process(auto_play)

func _collect_materials(node: Node) -> void:
	if node is CanvasItem and node.material is ShaderMaterial:
		node.material.set_shader_parameter("animate", false)
		_materials.append(node.material)
	for child in node.get_children(): _collect_materials(child)

func bind_background(id: String) -> void:
	var texture := load("res://BlackFlowSpire/Images/Events/Exit/%s.png" % id)
	for path in ["choice_scene_bg", "cg_light_outline", "cg_light_outline2", "cg_add"]:
		get_node("group_cg/%s/Image" % path).texture = texture

func _process(delta: float) -> void:
	_ambient_time += delta
	seek_entry(minf(_time + delta, DURATION))
	for material in _materials: material.set_shader_parameter("preview_time", _ambient_time)

func seek_entry(at: float) -> void:
	_time = at
	if _data.is_empty(): return
	for material in _materials: material.set_shader_parameter("preview_time", at)
	for track: Dictionary in _data.animation.tracks:
		var target := self if str(track.path).is_empty() else get_node_or_null(NodePath(track.path)) as Control
		if target == null: continue
		var value: Variant = _sample(track, at)
		var attribute: String = track.attribute
		var binding: Dictionary = _data.bindings[track.path]
		if attribute == "m_ScaleCurves": target.scale = Vector2(value.x, value.y)
		elif attribute == "m_EulerCurves": target.rotation = -deg_to_rad(float(value.z))
		elif attribute == "m_RotationCurves": target.rotation = -2.0 * atan2(float(value.z), float(value.w))
		elif attribute == "m_Alpha": target.modulate.a = clampf(float(value), 0.0, 1.0)
		elif attribute == "m_IsActive": target.visible = float(value) > 0.5
		elif attribute == "m_AnchoredPosition.x": target.position.x = binding.position[0] + float(value) - binding.anchored_position[0]
		elif attribute == "m_AnchoredPosition.y": target.position.y = binding.position[1] - float(value) + binding.anchored_position[1]
		elif attribute.begins_with("m_Color."):
			var image := target.get_node_or_null("Image") as CanvasItem
			if not image: continue
			var channel := "rgba".find(attribute.right(1))
			if image.material is ShaderMaterial:
				var tint: Variant = image.material.get_shader_parameter("control_color")
				if tint == null: tint = Color.WHITE
				tint[channel] = float(value)
				image.material.set_shader_parameter("control_color", tint)
			else:
				var tint := image.self_modulate
				tint[channel] = float(value)
				image.self_modulate = tint

func _number(value: Variant) -> float:
	if value is String: return -INF if value.begins_with("-") else INF
	return float(value)

func _sample(track: Dictionary, time: float) -> Variant:
	var keys: Array = track["keys"]
	if time <= float(keys[0].time): return keys[0].value
	if time >= float(keys[-1].time): return keys[-1].value
	for index in range(keys.size() - 1):
		var a: Dictionary = keys[index]
		var b: Dictionary = keys[index + 1]
		if time > float(b.time): continue
		if track.attribute == "m_IsActive": return a.value
		var duration := float(b.time) - float(a.time)
		var t := (time - float(a.time)) / duration
		if a.value is Dictionary:
			var result := {}
			for axis in a.value: result[axis] = _hermite(a.value[axis], b.value[axis], a.outSlope[axis], b.inSlope[axis], t, duration)
			return result
		return _hermite(a.value, b.value, a.outSlope, b.inSlope, t, duration)
	return keys[-1].value

func _hermite(start: Variant, end: Variant, outgoing: Variant, incoming: Variant, t: float, duration: float) -> float:
	var a := float(start)
	var b := float(end)
	var slope_a := _number(outgoing)
	var slope_b := _number(incoming)
	if is_inf(slope_a) or is_inf(slope_b): return a
	return (2*t*t*t - 3*t*t + 1)*a + (t*t*t - 2*t*t + t)*slope_a*duration + (-2*t*t*t + 3*t*t)*b + (t*t*t-t*t)*slope_b*duration
