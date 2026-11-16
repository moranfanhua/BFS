extends Control
@export var details_entry := false
@export var side_panel_start: float = -1.0
var _side_title: Label
var _side_description: Label
var _side_rules: Label
var _side_band: Control
var _side_info: Control
var _side_intel: Control
var _side_base: Dictionary
var _text_layout_queued := false
const INFO_COLORS := {
	"grey": Color("444444"), "cyan": Color("7ecacc"),
	"green": Color("005c32"), "final_green": Color("698e00"),
	"purple": Color("6b4c88"), "red": Color("980000"), "yellow": Color("fab619")
}

func _ready() -> void:
	resized.connect(_layout)
	if side_panel_start >= 0.0:
		_side_title = get_node("%NodeTitle") as Label
		_side_description = get_node("%NodeDescription") as Label
		_side_rules = get_node("%NodeRules") as Label
		_side_band = get_node("Canvas/TitleBand") as Control
		_side_info = get_node("Canvas/InfoBand") as Control
		_side_intel = get_node("%EnemyIntelligence") as Control
		_side_base = {
			"title_height": _side_title.size.y,
			"band_height": _side_band.size.y,
			"info_y": _side_info.position.y,
			"description_y": _side_description.position.y,
			"description_height": _side_description.size.y,
			"intel_y": _side_intel.position.y
		}
		_side_title.minimum_size_changed.connect(_queue_text_layout)
		_side_description.minimum_size_changed.connect(_queue_text_layout)
		_side_rules.minimum_size_changed.connect(_queue_text_layout)
	_layout()
	if details_entry:
		var canvas := get_node("Canvas") as Control
		var art := canvas.get_node("Artwork") as Control
		var target_position := art.position
		var target_scale := art.scale
		art.position = Vector2.ZERO
		art.scale = Vector2.ONE
		var transition := create_tween().set_parallel(true)
		transition.tween_property(art,"position",target_position,0.6).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		transition.tween_property(art,"scale",target_scale,0.6).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		for child in canvas.get_children():
			if child == art or child.name == "Dust" or not child is CanvasItem: continue
			var alpha: float = child.modulate.a
			child.modulate.a = 0
			transition.tween_property(child,"modulate:a",alpha,1.267)

func _layout() -> void:
	var canvas := get_node("Canvas") as Control
	var factor := minf(size.x/1280.0, size.y/720.0)
	canvas.scale = Vector2.ONE*factor
	canvas.position = (size-Vector2(1280,720)*factor)/2.0
	if side_panel_start >= 0.0:
		var background := get_node("Background") as Control
		var left := canvas.position.x + side_panel_start * factor
		background.position = Vector2(left, 0.0)
		background.size = Vector2(maxf(0.0, size.x-left), size.y)
		_queue_text_layout()

# Shared by runtime C# binding and the scene preview. No translated text lives here.
func set_visual_theme(theme_name: String, has_description: bool) -> void:
	if side_panel_start < 0.0: return
	_side_info.modulate = INFO_COLORS[theme_name]
	_side_info.visible = has_description
	var artwork_theme := "green" if theme_name == "final_green" else theme_name
	for pair in [["ThemeBloom",3], ["ThemeVine",2], ["ThemeSpots",1]]:
		var image := get_node("Canvas/"+str(pair[0])) as TextureRect
		image.texture = load("res://BlackFlowSpire/Images/Map/Details/Themes/deco_color%d_%s.png" % [pair[1],artwork_theme])
	_queue_text_layout()

func _queue_text_layout() -> void:
	if _text_layout_queued: return
	_text_layout_queued = true
	_layout_side_content.call_deferred()

func _layout_side_content() -> void:
	_text_layout_queued = false
	if not is_inside_tree() or not is_instance_valid(_side_title): return
	_side_title.size.y = maxf(float(_side_base.title_height), _side_title.get_combined_minimum_size().y)
	var title_bottom := _side_title.position.y + _side_title.size.y
	var band_bottom := _side_band.position.y + float(_side_base.band_height)
	var extra := maxf(0.0, title_bottom+10.0-band_bottom)
	_side_band.size.y = float(_side_base.band_height)+extra
	_side_info.position.y = float(_side_base.info_y)+extra
	_side_description.position.y = float(_side_base.description_y)+extra
	var description_minimum := 0.0 if _side_rules.visible else float(_side_base.description_height)
	_side_description.size.y = maxf(description_minimum, _side_description.get_combined_minimum_size().y)
	var content_bottom := _side_description.position.y + _side_description.size.y
	_side_rules.position.y = content_bottom + 12.0
	_side_rules.size.y = _side_rules.get_combined_minimum_size().y
	if _side_rules.visible:
		content_bottom = _side_rules.position.y + _side_rules.size.y
	_side_intel.position.y = maxf(float(_side_base.intel_y)+extra, content_bottom+15.0)
