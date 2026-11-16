@tool
extends Control

const DESIGN := Vector2(1422.222222, 800)
const EMPTY_DOT := preload("res://BlackFlowSpire/Images/Ui/Warehouse/dot-empty.png")
const OVER_DOT := preload("res://BlackFlowSpire/Images/Ui/Warehouse/dot-overflow.png")

func _ready() -> void:
	resized.connect(_layout)
	_layout()
	$Footer.set_expanded(true)
	$Canvas/Items.gui_input.connect(_scroll_input)
	var scrollbar := $Canvas/Items.get_h_scroll_bar() as HScrollBar
	var grabber := StyleBoxTexture.new()
	grabber.texture = preload("res://BlackFlowSpire/Images/Ui/Warehouse/scroll.png")
	grabber.texture_margin_left = 4
	grabber.texture_margin_right = 4
	scrollbar.add_theme_stylebox_override("scroll", StyleBoxEmpty.new())
	for style in ["grabber", "grabber_highlight", "grabber_pressed"]:
		scrollbar.add_theme_stylebox_override(style, grabber)
	scrollbar.custom_minimum_size.y = 8
	scrollbar.modulate.a = 0.5

func _layout() -> void:
	var factor := minf(size.x / DESIGN.x, size.y / DESIGN.y)
	$Canvas.scale = Vector2.ONE * factor
	$Canvas.position = Vector2((size.x - DESIGN.x * factor) / 2, size.y - DESIGN.y * factor)

func _scroll_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_WHEEL_DOWN or event.button_index == MOUSE_BUTTON_WHEEL_UP:
			$Canvas/Items.scroll_horizontal += 110 if event.button_index == MOUSE_BUTTON_WHEEL_DOWN else -110
			accept_event()

func bind_inventory(value: int, count: int, capacity: int, categories: Array, loaded_icon: String) -> void:
	$Canvas/Count/Current.text = str(count)
	$Canvas/Count/Capacity.text = str(capacity)
	$Canvas/Count/Current.modulate = Color(1, 0.55, 0.55) if count > capacity else Color.WHITE
	$Canvas/Estimate/Content/Value.text = str(value)
	$Footer.bind_box(value, count, capacity)
	$Canvas/Eye.bind_box(loaded_icon, count > capacity)
	$Canvas/EyeButton.disabled = loaded_icon.is_empty()
	for child in $Canvas/Dots.get_children():
		$Canvas/Dots.remove_child(child)
		child.queue_free()
	var slots := maxi(count, capacity)
	var columns := ceili(slots / 3.0)
	var left := (250.0 - columns * 33.0) / 2.0
	for index in slots:
		var texture: Texture2D = EMPTY_DOT
		if index < count:
			texture = OVER_DOT if index >= capacity else load("res://BlackFlowSpire/Images/Ui/Warehouse/dot-%s.png" % categories[index])
		var dot := TextureRect.new()
		dot.texture = texture
		dot.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		dot.size = texture.get_size()
		dot.position = Vector2(left + (index / 3) * 33, (index % 3) * 33) + (Vector2(33, 33) - dot.size) / 2
		dot.mouse_filter = Control.MOUSE_FILTER_IGNORE
		$Canvas/Dots.add_child(dot)

func bind_sort(by_value: bool, descending: bool) -> void:
	for button in [$Canvas/Sort/Type, $Canvas/Sort/Value]:
		var active: bool = (button == $Canvas/Sort/Value) == by_value
		button.get_node("Text").modulate = Color("00b1ff") if active else Color("949494")
		button.get_node("Text").position.y = 9.0 if active else 14.1
		button.get_node("Arrow").visible = active
		button.get_node("Arrow").rotation = PI if descending else 0.0
