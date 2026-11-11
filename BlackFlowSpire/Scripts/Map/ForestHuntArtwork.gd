extends Control

@export var auto_play := true
@export var show_confirmation := true
var _data: Dictionary
var _time := 0.0

func _ready() -> void:
	_data = JSON.parse_string(FileAccess.get_file_as_string("res://BlackFlowSpire/Data/Hunt/entry.json"))
	seek_entry(0.0 if auto_play else 2.5)
	set_process(auto_play)
	get_node("btn_certain").visible = show_confirmation
	if not show_confirmation: get_node("group_bg/grad_down").hide()

func _process(delta: float) -> void:
	_time = minf(_time + delta, 2.5)
	seek_entry(_time)
	if _time >= 2.5:
		set_process(false)

# Evaluate the exported Unity Hermite tangents instead of guessing easing curves.
func _sample(track: Dictionary, at: float) -> Variant:
	var keys: Array = track["keys"]
	if at <= float(keys[0].time): return keys[0].value
	if at >= float(keys[-1].time): return keys[-1].value
	for index in range(keys.size() - 1):
		var a: Dictionary = keys[index]
		var b: Dictionary = keys[index + 1]
		if at > float(b.time): continue
		if track.attribute == "m_IsActive": return a.value
		var duration := float(b.time) - float(a.time)
		var t := (at - float(a.time)) / duration
		if a.value is Dictionary:
			var result := {}
			for axis in a.value:
				result[axis] = _hermite(a.value[axis], b.value[axis], a.outSlope[axis], b.inSlope[axis], t, duration)
			return result
		return _hermite(a.value, b.value, a.outSlope, b.inSlope, t, duration)
	return keys[-1].value

func _hermite(a: float, b: float, out_slope: float, in_slope: float, t: float, duration: float) -> float:
	if is_inf(out_slope) or is_inf(in_slope): return a
	return (2*t*t*t - 3*t*t + 1)*a + (t*t*t - 2*t*t + t)*out_slope*duration + (-2*t*t*t + 3*t*t)*b + (t*t*t - t*t)*in_slope*duration

func seek_entry(at: float) -> void:
	_time = at
	if _data.is_empty(): return
	for track in _data.animation.tracks:
		var node := get_node_or_null(NodePath(track.path)) as Control
		if node == null: continue
		var binding: Dictionary = _data.bindings.get(track.path, {})
		var value: Variant = _sample(track, at)
		var attribute: String = track.attribute
		if attribute == "m_ScaleCurves":
			node.scale = Vector2(value.x, value.y)
		elif attribute == "m_Alpha":
			node.modulate.a = clampf(float(value), 0, 1)
		elif attribute == "m_IsActive":
			node.visible = float(value) > 0.5
		elif attribute.begins_with("m_AnchoredPosition"):
			if attribute.ends_with("x"): node.position.x = binding.position[0] + float(value) - binding.anchored_position[0]
			else: node.position.y = binding.position[1] - float(value) + binding.anchored_position[1]
		elif attribute == "m_SizeDelta.x":
			node.size.x = maxf(0, binding["size"][0] + float(value) - binding.size_delta[0])
			node.pivot_offset.x = node.size.x * binding.pivot[0]
		elif attribute.begins_with("m_Color."):
			var graphic := node.get_node_or_null("Image") as CanvasItem
			if graphic == null: graphic = node.get_node_or_null("Caption") as CanvasItem
			if graphic == null: continue
			var channel := "rgba".find(attribute.right(1))
			if graphic.material is ShaderMaterial and binding.get("shader", false):
				var control: Variant = graphic.material.get_shader_parameter("control_color")
				control[channel] = float(value)
				graphic.material.set_shader_parameter("control_color", control)
			else:
				var tint := graphic.self_modulate
				tint[channel] = float(value)
				graphic.self_modulate = tint
	var confirm := get_node_or_null("%ConfirmHunt") as Button
	if confirm != null: confirm.disabled = at < 2.5
