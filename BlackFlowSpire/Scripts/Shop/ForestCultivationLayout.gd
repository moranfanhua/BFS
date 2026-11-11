@tool
extends Control

func _ready() -> void:
	resized.connect(_layout)
	_layout()
	$Canvas/Playback/Results/Scroll.gui_input.connect(_scroll_input)

func _scroll_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_WHEEL_DOWN or event.button_index == MOUSE_BUTTON_WHEEL_UP:
			$Canvas/Playback/Results/Scroll.scroll_horizontal += 120 if event.button_index == MOUSE_BUTTON_WHEEL_DOWN else -120
			accept_event()

func _layout() -> void:
	var design := Vector2(1422.222222, 800)
	var factor := minf(size.x / design.x, size.y / design.y)
	$Canvas.scale = Vector2.ONE * factor
	$Canvas.position = (size - design * factor) / 2.0

func set_stage(stage: int) -> void:
	$Canvas/Entry.visible = stage == 0
	$Canvas/Playback.visible = stage != 0
	$Canvas/Playback/Growth.visible = stage == 1
	$Canvas/Playback/Skip.visible = stage == 1
	$Canvas/Playback/Results.visible = stage == 2

func show_results() -> void:
	set_stage(2)
	var result := $Canvas/Playback/Results as Control
	result.modulate.a = 0.0
	var tween := create_tween()
	tween.tween_property(result, "modulate:a", 1.0, 0.5)
