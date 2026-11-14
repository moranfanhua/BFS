@tool
extends Control

const DESIGN_SIZE := Vector2(1422.222222, 800.0)
const ROW_HEIGHT := 64.0
const MASK_HEIGHT := 161.0
const HOPE_BAR_LENGTH := 60.0
const HOPE_LABEL_OFFSET := 14.0
const HOPE_LABEL_GAP := 12.0
const HOPE_FONT_SIZE := 26

var _hope_cost := 0.0
var _hope_maximum := 0.0
var _hope_tween: Tween

func _ready() -> void:
	if not Engine.is_editor_hint():
		_apply_scalable_fonts()
	resized.connect(_layout)
	_layout()

func _apply_scalable_fonts() -> void:
	var fonts: Dictionary[FontVariation, FontVariation] = {}
	for node in $Canvas.find_children("*", "Label", true, false):
		var label := node as Label
		var source := label.get_theme_font("font") as FontVariation
		if source == null or not source.base_font is FontFile:
			continue
		if not fonts.has(source):
			# Raster glyphs are otherwise enlarged with the whole HUD canvas.
			var scalable := source.duplicate() as FontVariation
			var face := source.base_font.duplicate() as FontFile
			face.multichannel_signed_distance_field = true
			scalable.base_font = face
			fonts[source] = scalable
		label.add_theme_font_override("font", fonts[source])

func _layout() -> void:
	var factor := minf(size.x / DESIGN_SIZE.x, size.y / DESIGN_SIZE.y)
	if factor <= 0.0:
		return
	$Canvas.scale = Vector2.ONE * factor
	$Canvas.size = Vector2(size.x / factor, ROW_HEIGHT)
	$TopMask.offset_bottom = MASK_HEIGHT * factor

func set_hope(balance: int, maximum: int, animate: bool) -> void:
	if _hope_tween != null:
		_hope_tween.kill()
	var cost := float(maximum - balance)
	if not animate or _hope_cost <= 0.0 or _hope_maximum <= 0.0 or maximum <= 0:
		_draw_hope(cost, maximum, maximum)
		return
	var start_cost := _hope_cost
	var start_maximum := _hope_maximum
	var update := func(weight: float) -> void:
		_draw_hope(lerpf(start_cost, cost, weight), lerpf(start_maximum, maximum, weight), maximum)
	_hope_tween = create_tween().set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	_hope_tween.tween_method(update, 0.0, 1.0, 1.0)

func _draw_hope(cost: float, maximum: float, target_maximum: float) -> void:
	_hope_cost = clampf(cost, 0.0, maximum)
	_hope_maximum = maximum
	var balance := maximum - _hope_cost
	var units_per_hope := HOPE_BAR_LENGTH / target_maximum if target_maximum > 0.0 else 0.0
	var bar := $Canvas/Hope as TextureRect
	var capacity := $Canvas/Hope/Capacity as TextureRect
	var fill := $Canvas/Hope/Fill as TextureRect
	var balance_label := $Canvas/Hope/Balance as Label
	var maximum_label := $Canvas/Hope/Maximum as Label
	capacity.offset_right = capacity.offset_left + maximum * units_per_hope
	fill.offset_right = fill.offset_left + balance * units_per_hope
	balance_label.text = str(roundi(balance))
	maximum_label.text = str(roundi(maximum))
	var balance_left := fill.offset_right + HOPE_LABEL_OFFSET
	var maximum_left := capacity.offset_right + HOPE_LABEL_OFFSET
	var font := balance_label.get_theme_font("font")
	var balance_width := font.get_string_size(balance_label.text, HORIZONTAL_ALIGNMENT_LEFT, -1, HOPE_FONT_SIZE).x
	var maximum_width := font.get_string_size(maximum_label.text, HORIZONTAL_ALIGNMENT_LEFT, -1, HOPE_FONT_SIZE).x
	var maximum_fit := (bar.size.x - maximum_left) / maximum_width
	var adjacent_fit := (bar.size.x - balance_left - HOPE_LABEL_GAP) / (balance_width + maximum_width)
	var font_size := clampi(floori(HOPE_FONT_SIZE * minf(maximum_fit, adjacent_fit)), 1, HOPE_FONT_SIZE)
	balance_label.add_theme_font_size_override("font_size", font_size)
	maximum_label.add_theme_font_size_override("font_size", font_size)
	balance_width = font.get_string_size(balance_label.text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x
	maximum_width = font.get_string_size(maximum_label.text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x
	balance_label.position.x = balance_left
	balance_label.size.x = balance_width
	maximum_label.position.x = maxf(maximum_left, balance_left + balance_width + HOPE_LABEL_GAP)
	maximum_label.size.x = maximum_width
