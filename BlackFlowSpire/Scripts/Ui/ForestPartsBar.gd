@tool
extends Control

const DESIGN_SIZE := Vector2(1422.222222, 800.0)
const BAR_HEIGHT := 84.0
var _phase := 0.0
var _expanded := false

static func bar_height(viewport_size: Vector2) -> float:
	return BAR_HEIGHT * minf(viewport_size.x / DESIGN_SIZE.x, viewport_size.y / DESIGN_SIZE.y)

func _ready() -> void:
	resized.connect(_layout)
	_layout()

func _layout() -> void:
	var factor := minf(size.x / DESIGN_SIZE.x, size.y / DESIGN_SIZE.y)
	$Canvas.scale = Vector2.ONE * factor
	$Canvas.position = Vector2((size.x - DESIGN_SIZE.x * factor) / 2.0, size.y - BAR_HEIGHT * factor)
	$Backdrop.offset_top = -BAR_HEIGHT * factor
	if factor > 0.0:
		var window := $Canvas/CollectiblesWindow as Control
		window.position = -$Canvas.position / factor
		window.size = Vector2(size.x, $Canvas.position.y) / factor

func bind_box(value: int, count: int, capacity: int) -> void:
	$Canvas/PartsBox/Value/Amount.text = str(value)
	$Canvas/PartsBox/Count/Current.text = str(count)
	$Canvas/PartsBox/Count/Capacity.text = str(capacity)
	var overloaded := count > capacity
	$Canvas/PartsBox/Full.visible = count == capacity
	$Canvas/PartsBox/Overloaded.visible = overloaded
	$Canvas/PartsBox/OverloadGlow.visible = overloaded
	$Canvas/PartsBox/Connection.visible = not overloaded
	$Canvas/PartsBox/Count/Current.modulate = Color(1.0, 0.57, 0.57) if overloaded else Color.WHITE

func set_expanded(expanded: bool) -> void:
	_expanded = expanded
	$Canvas/PartsBox/Collapse.visible = expanded
	for path in ["Value", "ValueTitle", "BoxTitle", "Count", "Eye", "Full", "Overloaded"]:
		get_node("Canvas/PartsBox/" + path).modulate.a = 0.35 if expanded else 1.0

func _process(delta: float) -> void:
	if Engine.is_editor_hint() or not is_visible_in_tree(): return
	_phase = fmod(_phase + delta, TAU)
	var strength := 0.35 if _expanded else 1.0
	$Canvas/PartsBox/Connection.modulate.a = (0.88 + 0.12 * sin(_phase * 1.5)) * strength
	$Canvas/PartsBox/OverloadGlow.modulate.a = (0.82 + 0.18 * sin(_phase * 2.0)) * strength
