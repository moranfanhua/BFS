extends Control

const PartsBarLayout = preload("res://BlackFlowSpire/Scripts/Ui/BlackFlowSpirePartsBar.gd")
const TopBarLayout = preload("res://BlackFlowSpire/Scripts/Ui/BlackFlowSpireTopBar.gd")
const HUD_GAP := 12.0

@export var top_inset := 0.0
@export var bottom_inset := 50.0
@export var grid_spacing := Vector2(176.0,128.0)
var _layout_queued := false
var _movement: Control
var _details: Control
var _debug_labels: Array[Control] = []

func _ready() -> void:
	get_viewport().size_changed.connect(refresh_layout)
	if get_parent() is Control: get_parent().resized.connect(refresh_layout)
	refresh_layout()

func refresh_layout() -> void:
	if _layout_queued: return
	_layout_queued = true
	_fit_viewport.call_deferred()

func bind_movement_layout(widget: Control, debug_info: Control) -> void:
	_movement = widget
	_details = get_node_or_null("BlackFlowSpireNodeDetails") as Control
	if _details: _details.visibility_changed.connect(refresh_layout)
	if debug_info:
		for node in debug_info.find_children("*", "Label", true, false):
			var label := node as Control
			_debug_labels.append(label)
			label.visibility_changed.connect(refresh_layout)
			label.item_rect_changed.connect(refresh_layout)
	refresh_layout()

func _fit_viewport() -> void:
	_layout_queued = false
	if not is_inside_tree(): return
	# NMapScreen can extend beyond the viewport. Map its visible bounds back into
	# parent coordinates so the map and background never inherit that crop.
	var area := get_viewport_rect()
	var parent := get_parent() as Control
	if parent:
		var inverse := parent.get_global_transform_with_canvas().affine_inverse()
		var start := inverse*area.position
		var end := inverse*area.end
		# Reserve the same height as the shared footer, including scaled UI sizes.
		bottom_inset = PartsBarLayout.bar_height(area.size) * (end.y - start.y) / area.size.y
		set_anchors_preset(Control.PRESET_TOP_LEFT)
		position = start+Vector2(0.0,top_inset)
		size = Vector2(maxf(1.0,end.x-start.x),maxf(1.0,end.y-start.y-top_inset-bottom_inset))
	$BottomReserved.offset_bottom=bottom_inset
	_layout_movement(minf(size.x / TopBarLayout.DESIGN_SIZE.x, (size.y + bottom_inset) / TopBarLayout.DESIGN_SIZE.y))

func _layout_movement(factor: float) -> void:
	if not is_instance_valid(_movement) or factor <= 0.0: return
	var footprint := Rect2(Vector2.ZERO, _movement.size)
	for child in _movement.get_children():
		if child is Control and child.visible:
			footprint = footprint.merge(Rect2(child.position, child.size * child.scale))
	var gap := HUD_GAP * factor
	var top := TopBarLayout.ROW_HEIGHT * factor + gap
	var right := size.x - 24.0 * factor
	if is_instance_valid(_details) and _details.visible:
		right = minf(right, _details.position.x + _details.get_node("Background").position.x - gap)
	var candidate := Rect2(Vector2(right - footprint.size.x * factor, top), footprint.size * factor)
	var to_map := get_global_transform_with_canvas().affine_inverse()
	for label in _debug_labels:
		if not is_instance_valid(label) or not label.is_visible_in_tree(): continue
		var bounds := to_map * (label.get_global_transform_with_canvas() * Rect2(Vector2.ZERO, label.size))
		if candidate.grow(gap).intersects(bounds):
			candidate.position.x = bounds.position.x - gap - candidate.size.x
	_movement.scale = Vector2.ONE * factor
	_movement.position = Vector2(maxf(0.0, candidate.position.x), top) - footprint.position * factor
