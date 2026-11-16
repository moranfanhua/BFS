@tool
extends Control

const DESIGN_SIZE := Vector2(1422.222222, 800.0)
const ROW_HEIGHT := 64.0
const MASK_HEIGHT := 161.0

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
