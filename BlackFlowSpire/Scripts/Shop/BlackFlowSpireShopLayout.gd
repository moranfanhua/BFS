@tool
extends Control

# Unity's source canvas uses 800 reference units at a 16:9 aspect ratio.
const DESIGN_SIZE := Vector2(1422.222222, 800)

func _ready() -> void:
	resized.connect(_layout)
	_layout()
	layout_dialogue.call_deferred()

func _layout() -> void:
	var canvas := get_node_or_null("Canvas") as Control
	if not canvas: return
	var factor := minf(size.x / DESIGN_SIZE.x, size.y / DESIGN_SIZE.y)
	canvas.scale = Vector2.ONE * factor
	canvas.position = (size - DESIGN_SIZE * factor) / 2.0

func layout_dialogue() -> void:
	_fit_label("Canvas/GoodsViewport/Padding/Grid/Investment/Title", 20)
	_fit_label("Canvas/GoodsViewport/Padding/Grid/Garden/Title", 20)
	_fit_label("Canvas/Refresh/Text", 19)
	var text := get_node_or_null("Canvas/Dialogue/Text") as Label
	var background := get_node_or_null("Canvas/Dialogue/Background") as TextureRect
	if not text or not background: return
	# The green name strip stays fixed. Grow only the dark body for longer lines,
	# rather than leaving the name underneath the strip or clipping the dialogue.
	text.size.y = maxf(32.0, text.get_minimum_size().y)
	background.size.y = maxf(112.0, text.position.y + text.size.y + 12.0 - background.position.y)

func _fit_label(path: String, base_size: int) -> void:
	var label := get_node_or_null(path) as Label
	if label == null: return
	var width := label.get_theme_font("font").get_string_size(label.text, HORIZONTAL_ALIGNMENT_LEFT, -1, base_size).x
	label.add_theme_font_size_override("font_size", clampi(floori(base_size * maxf(1.0, label.size.x - 4.0) / maxf(1.0, width)), 12, base_size))
