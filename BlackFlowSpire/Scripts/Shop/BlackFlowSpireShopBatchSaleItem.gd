extends Control

@onready var _description: RichTextLabel = $Description

func _ready() -> void:
	resized.connect(_fit_description)

func bind_item(title: String, description: String, icon_id: String, category: String, uses: int, max_uses: int, loaded: bool, gold: int) -> void:
	$Title.text = title
	_description.text = description
	_fit_description()
	$Price/Value.text = str(gold)
	$Icon.bind_part(icon_id, category, uses, max_uses, loaded)
	# Keep the native capsule corners while allowing larger adapted gold prices.
	var amount := $Price/Value as Label
	var width := amount.get_theme_font("font").get_string_size(str(gold), HORIZONTAL_ALIGNMENT_LEFT, -1, 21).x
	$Price.size.x = maxf(97.9, 57.0 + width)
	amount.size.x = $Price.size.x - amount.position.x - 12.0

func _fit_description() -> void:
	custom_minimum_size.y = maxf(145.0, _description.position.y + _description.get_content_height() - _description.offset_bottom)
