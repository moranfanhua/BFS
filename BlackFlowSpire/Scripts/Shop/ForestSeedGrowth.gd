extends Control

# Original unweighted Hermite keys from plant_tree_show; no reroll or reward work
# is performed by this view. Paths and property names are those of the prefab.
var _channels: Array = []
var _targets: Dictionary = {}
var _positions: Dictionary = {}
var _materials: Array[ShaderMaterial] = []

func _ready() -> void:
	var clip: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://BlackFlowSpire/Animations/Shop/cultivation.json"))
	_channels = clip.tracks
	for track: Dictionary in _channels:
		if _targets.has(track.path): continue
		var target := get_node_or_null(NodePath(track.path)) as Control
		if target:
			_targets[track.path] = target
			_positions[track.path] = target.position
	_prepare_pivots(self)

func _prepare_pivots(node: Node) -> void:
	if node is Control and node.has_meta("unity_pivot"):
		node.pivot_offset = node.size * node.get_meta("unity_pivot")
	if node is CanvasItem and node.material is ShaderMaterial and not _materials.has(node.material):
		_materials.append(node.material)
	for child in node.get_children(): _prepare_pivots(child)

func sample(time: float) -> void:
	for material in _materials: material.set_shader_parameter("elapsed",time)
	for track: Dictionary in _channels:
		var node: Control = _targets.get(track.path)
		if not node: continue
		if track.kind == "m_ScaleCurves":
			node.scale = Vector2(_evaluate(track.keys, time, "x"), _evaluate(track.keys, time, "y"))
			continue
		var value := _evaluate(track.keys, time)
		var property: String = track.attribute
		if property.begins_with("m_Color."):
			var sprite := node.get_node_or_null("Sprite") as CanvasItem
			if not sprite: sprite = node.get_node_or_null("Text") as CanvasItem
			if sprite:
				var color := sprite.self_modulate
				color[property.trim_prefix("m_Color.")] = value
				sprite.self_modulate = color
		elif property == "m_Alpha":
			node.modulate.a = value
		elif property == "m_IsActive":
			node.visible = value > 0.5
		elif property == "m_AnchoredPosition.y":
			node.position.y = _positions[track.path].y - value + float(node.get_meta("unity_y", 0.0))
		elif property == "InitialModule.startSpeed.scalar":
			var particles := node.get_node_or_null("Particles") as CPUParticles2D
			if particles: particles.initial_velocity_max = value * 100.0

func start_particles() -> void:
	_particles(self, true)

func stop_particles() -> void:
	_particles(self, false)

func _particles(node: Node, active: bool) -> void:
	if node is CPUParticles2D:
		node.emitting = active
		if active: node.restart()
	for child in node.get_children(): _particles(child, active)

func _number(key: Dictionary, field: String, axis: String) -> float:
	var value = key[field]
	if not axis.is_empty(): value = value[axis]
	if value is String: return -INF if value.begins_with("-") else INF
	return float(value)

func _evaluate(keys: Array, time: float, axis: String = "") -> float:
	if time <= float(keys[0].time): return _number(keys[0], "value", axis)
	if time >= float(keys[-1].time): return _number(keys[-1], "value", axis)
	var lo := 0
	var hi := keys.size() - 1
	while hi - lo > 1:
		var mid := (lo + hi) / 2
		if float(keys[mid].time) <= time: lo = mid
		else: hi = mid
	var first: Dictionary = keys[lo]
	var last: Dictionary = keys[hi]
	var slope_out := _number(first, "outSlope", axis)
	var slope_in := _number(last, "inSlope", axis)
	var a := _number(first, "value", axis)
	if not is_finite(slope_out) or not is_finite(slope_in): return a
	var dt := float(last.time) - float(first.time)
	var u := (time - float(first.time)) / dt
	var u2 := u * u
	var u3 := u2 * u
	return (2*u3-3*u2+1)*a + (u3-2*u2+u)*dt*slope_out + (-2*u3+3*u2)*_number(last,"value",axis) + (u3-u2)*dt*slope_in
