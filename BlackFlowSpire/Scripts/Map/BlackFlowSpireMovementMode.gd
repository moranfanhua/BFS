extends Control

const ROW := preload("res://BlackFlowSpire/Scenes/Map/BlackFlowSpireMovementModeRow.tscn")
const DESIGN_SIZE := Vector2(1536, 864)
const SIDE_WIDTH := 512.0
const DURATION := 0.16

var _slide := 20.0

func _ready() -> void:
	resized.connect(_layout)
	$Panel.modulate.a = 0.0
	_layout()

func _layout() -> void:
	var factor := maxf(0.001, minf(size.x / DESIGN_SIZE.x, size.y / DESIGN_SIZE.y))
	var height := size.y / factor
	$Panel.scale = Vector2.ONE * factor
	$Panel.position.x = size.x - (SIDE_WIDTH - _slide) * factor
	$Panel.size.y = height
	$Panel/Backdrop.size.y = height
	$Panel/Rows.size.y = height - $Panel/Rows.position.y
	$Confirm/Card.scale = Vector2.ONE * factor
	$Confirm/Card.position = (size - $Confirm/Card.size * factor) / 2.0

func _offset(value: float) -> void:
	_slide = value
	_layout()

func animate(opening: bool) -> Tween:
	var animation := create_tween().set_parallel(true).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	animation.tween_method(_offset, _slide, 0.0 if opening else 20.0, DURATION)
	animation.tween_property($Panel, "modulate:a", 1.0 if opening else 0.0, DURATION)
	return animation

func bind_header(title: String, caption: String, points: int, warning: String, finish: String, message: String, cancel: String) -> void:
	$Panel/Title.text = title
	$Panel/PointCaption.text = caption
	$Panel/Points.text = str(points)
	$Panel/Warning.text = warning
	$Panel/Finish/Text.text = finish
	$Panel/Finish.tooltip_text = finish
	$Confirm/Card/Message.text = message
	$Confirm/Card/Cancel.text = cancel
	$Confirm/Card/Accept/Text.text = finish

func add_mode(title: String, description: String, icon: String, uses: int, maximum: int, remaining: String, equipped: String) -> Button:
	var row := ROW.instantiate() as Button
	$Panel/Rows/List.add_child(row)
	row.bind_mode(title, description, icon, uses, maximum, remaining, equipped)
	return row
