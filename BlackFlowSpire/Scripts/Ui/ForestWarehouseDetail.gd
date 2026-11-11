@tool
extends Control

const PANEL_SIZE := Vector2(961, 574)
var _entrance: Tween
var _switch: Tween
var _actions: Tween
var _shift := 0.0
var _zoom := 1.0

func _ready() -> void:
	resized.connect(_layout)
	visibility_changed.connect(_reset_when_hidden)
	_layout()

func _layout() -> void:
	var factor := minf(size.x / 1422.222222, size.y / 800.0)
	$Panel.scale = Vector2.ONE * factor * _zoom
	$Panel.position = (size - PANEL_SIZE * factor * _zoom) / 2.0 + Vector2(_shift * factor, 0)
	$Backdrop.position = Vector2.ZERO
	$Backdrop.size = Vector2(size.x, maxf(1, size.y - 84 * factor))

func bind_part(title: String, description: String, icon: String, category: String, uses: int, maximum: int,
		loaded: bool, value: int, category_title: String = "", uses_text: String = "", flavor: String = "") -> void:
	$Panel/Title.text = title
	$Panel/Category.text = category_title
	$Panel/Description.text = description if flavor.is_empty() else description + "\n\n[color=#a6a6a6][i]" + flavor + "[/i][/color]"
	$Panel/Price/Amount.text = str(value)
	$Panel/Icon.bind_part(icon, category, uses, maximum, loaded)
	$Panel/CategoryIcon.texture = $Panel/Icon/Type.texture
	$Panel/Icon/Type.hide()
	$Panel/Icon/Uses.hide()
	$Panel/Uses.visible = maximum > 0
	$Panel/Uses/Text.text = uses_text
	$Panel/CategoryGlow.visible = category == "processed"
	$Panel/Actions/Equip/Background.texture = load("res://BlackFlowSpire/Images/Ui/Warehouse/unequip.png" if loaded else "res://BlackFlowSpire/Images/Ui/Warehouse/equip.png")

func enter() -> void:
	_restore_animation_state()
	_entrance = create_tween().set_parallel()
	_entrance.tween_property($Backdrop, "modulate:a", 1.0, 0.2333).from(0.0)
	for item in [$Panel/Background, $Panel/Roots, $Panel/Light, $Panel/Plants, $Panel/Specks, $Panel/Evolve, $Panel/Dust, $Panel/CategoryDust]:
		_entrance.tween_property(item, "self_modulate:a" if item is TextureRect else "modulate:a", 1.0, 1.2).from(0.0)
	for item in [$Panel/Icon, $Panel/Category, $Panel/CategoryIcon, $Panel/CategoryGlow, $Panel/Title,
			$Panel/NameBackdrop, $Panel/Description, $Panel/DescriptionBackdrop, $Panel/Price, $Panel/Uses]:
		_entrance.tween_property(item, "self_modulate:a" if item is TextureRect else "modulate:a", 1.0, 0.6667).from(0.0)
	_entrance.tween_property($Panel/LeafLeft, "scale", Vector2.ONE, 0.9).from(Vector2.ONE * 0.77019).set_delay(0.0667)
	_entrance.tween_property($Panel/LeafRight, "scale", Vector2.ONE, 0.9).from(Vector2.ONE * 0.83206).set_delay(0.0667)
	for item in [$Panel/LeafLeft, $Panel/LeafRight]:
		_entrance.tween_property(item, "self_modulate:a", 1.0, 1.1333).from(0.0)
	_entrance.tween_property($Panel/Actions, "position:y", 384.0, 0.6).from(431.8).set_delay(0.15)
	_entrance.tween_property($Panel/Actions, "modulate:a", 1.0, 0.6).from(0.0).set_delay(0.15)
	for arrow in [$Panel/Previous, $Panel/Next]:
		_entrance.tween_property(arrow, "modulate:a", 1.0, 0.4334).from(0.0).set_delay(0.2333)
	_entrance.tween_interval(1.5)

func is_switching() -> bool:
	return _switch != null and _switch.is_running()

func switch_part(direction: int, update: Callable) -> void:
	if is_switching(): return
	_restore_animation_state()
	_switch = create_tween()
	_switch.tween_method(_set_motion, Vector2(0, 1), Vector2(-40 * direction, 0.97), 0.25)
	_switch.parallel().tween_property($Panel, "modulate:a", 0.0, 0.25)
	_switch.tween_callback(update)
	_switch.tween_method(_set_motion, Vector2(45 * direction, 0.97), Vector2(0, 1), 0.4167).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	_switch.parallel().tween_property($Panel, "modulate:a", 1.0, 0.4167)

func layout_actions(animate: bool) -> void:
	var discard := $Panel/Actions/Discard as Button
	var target := 227.0 if $Panel/Actions/Equip.visible else (PANEL_SIZE.x - discard.size.x) / 2.0
	if _actions: _actions.kill()
	if animate and not is_equal_approx(discard.position.x, target):
		_actions = create_tween()
		_actions.tween_property(discard, "position:x", target, 0.6667).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN_OUT)
	else:
		discard.position.x = target

func _set_motion(value: Vector2) -> void:
	_shift = value.x
	_zoom = value.y
	_layout()

func _reset_when_hidden() -> void:
	if not is_visible_in_tree():
		_restore_animation_state()

func _restore_animation_state() -> void:
	if _entrance: _entrance.kill()
	if _switch: _switch.kill()
	if _actions: _actions.kill()
	_set_motion(Vector2(0, 1))
	$Backdrop.modulate.a = 1.0
	$Panel.modulate.a = 1.0
	for item in $Panel.get_children():
		if item is CanvasItem:
			item.self_modulate.a = 1.0
			if not item is TextureRect: item.modulate.a = 1.0
	$Panel/Actions.position.y = 384.0
	$Panel/LeafLeft.scale = Vector2.ONE
	$Panel/LeafRight.scale = Vector2.ONE
