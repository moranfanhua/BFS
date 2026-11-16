extends Control

@export var minimum_font_size := 14
var _base_font_size := 20

func _ready() -> void:
	_base_font_size=$Row/Name.get_theme_font_size("font_size")
	resized.connect(_fit_text)

# Text comes from the visible node ID and its localization, never a hidden stage.
func bind_caption(caption: String, completed: bool, reachable: bool) -> void:
	visible=not caption.is_empty()
	$Row/Name.text=caption
	$Row/Completed.visible=completed
	set_reachable(reachable)
	_fit_text.call_deferred()

func set_reachable(reachable: bool) -> void:
	$Row/Name.add_theme_color_override("font_color",Color("edf2ef") if reachable else Color("a5aca8"))

func _fit_text() -> void:
	if not is_inside_tree(): return
	var label := $Row/Name as Label
	var available := maxf(1.0,size.x-(28.0 if $Row/Completed.visible else 0.0))
	var font := label.get_theme_font("font")
	var width := font.get_string_size(label.text,HORIZONTAL_ALIGNMENT_LEFT,-1,_base_font_size).x+3.0
	var target := clampi(int(floor(_base_font_size*available/maxf(1.0,width))),minimum_font_size,_base_font_size)
	label.add_theme_font_size_override("font_size",target)
	var fitted := font.get_string_size(label.text,HORIZONTAL_ALIGNMENT_LEFT,-1,target).x+3.0
	label.custom_minimum_size.x=minf(available,fitted)
