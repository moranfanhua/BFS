extends Control

const ART := "res://BlackFlowSpire/Images/Ui/Warehouse/eye-%s.png"
const LAYERS := ["bg", "ball", "iris", "frame"]
var _phase := 0.0
var _overloaded := false
var _recovery: Tween
var _old_layers: Array[TextureRect] = []
@onready var _layers: Array[TextureRect] = [$Background, $Ball, $Iris, $Frame]

func bind_box(icon: String, overloaded: bool) -> void:
	if _overloaded != overloaded:
		_finish_recovery()
		if _overloaded: _play_recovery()
		_overloaded = overloaded
		for index in _layers.size():
			_layers[index].texture = load(ART % (LAYERS[index] + ("-overflow" if overloaded else "")))
	var has_item := not icon.is_empty()
	$Item.visible = has_item
	if has_item:
		$Item.texture = load("res://BlackFlowSpire/Images/Parts/%s.png" % icon)
		if not $AnimationPlayer.is_playing(): $AnimationPlayer.play("item_float")
	else:
		$AnimationPlayer.stop()

func _play_recovery() -> void:
	_recovery = create_tween().set_parallel()
	for layer in _layers:
		var previous := layer.duplicate() as TextureRect
		add_child(previous)
		move_child(previous, layer.get_index() + 1)
		_old_layers.append(previous)
		var is_background := layer == $Background
		_recovery.tween_property(previous, "modulate:a", 0.0, 0.3 if is_background else 0.4833).set_delay(0.3333 if is_background else 0.3)
		_recovery.tween_property(layer, "self_modulate:a", 1.0, 0.2333).from(0.0).set_delay(0.1)
	$RecoveryFlash.show()
	$RecoveryFlash.modulate = Color(0.98, 1, 0, 0)
	_recovery.tween_property($RecoveryFlash, "modulate", Color(0.98, 0, 1, 0.85), 1.0167)
	_recovery.chain().tween_property($RecoveryFlash, "modulate:a", 0.0, 0.5666)
	_recovery.chain().tween_callback(_clear_previous_layers)

func _clear_previous_layers() -> void:
	for layer in _old_layers:
		layer.hide()
		layer.queue_free()
	_old_layers.clear()
	$RecoveryFlash.hide()

func _finish_recovery() -> void:
	if _recovery: _recovery.kill()
	_clear_previous_layers()
	for layer in _layers:
		layer.self_modulate.a = 1.0

func _process(delta: float) -> void:
	if not is_visible_in_tree(): return
	_phase = fmod(_phase + delta, TAU)
	$Iris.modulate.a = 0.85 + 0.15 * sin(_phase)
	$Specks.modulate.a = 0.25 + 0.12 * sin(_phase * 0.7)
	$SpecksNear.modulate.a = 0.32 + 0.1 * cos(_phase * 0.65)
	$DownGlow.modulate.a = 0.35 + 0.1 * sin(_phase * 0.55)
