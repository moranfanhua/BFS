extends Control

const PartsBarLayout = preload("res://BlackFlowSpire/Scripts/Ui/BlackFlowSpirePartsBar.gd")
const TopBarLayout = preload("res://BlackFlowSpire/Scripts/Ui/BlackFlowSpireTopBar.gd")
# rl06_zone_action_view: top-right anchor, center Y at -141.533 and height 149.066.
const ACTION_POINTS_TOP := 67.0

@export var top_inset := 0.0
@export var bottom_inset := 50.0
@export var grid_spacing := Vector2(176.0,128.0)
var _layout_queued := false
var _movement: Control

func _ready() -> void:
	get_viewport().size_changed.connect(refresh_layout)
	if get_parent() is Control: get_parent().resized.connect(refresh_layout)
	refresh_layout()

func refresh_layout() -> void:
	if _layout_queued: return
	_layout_queued = true
	_fit_viewport.call_deferred()

func bind_movement_layout(widget: Control) -> void:
	_movement = widget
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
	_movement.scale = Vector2.ONE * factor
	_movement.position = Vector2(size.x - _movement.size.x * factor, ACTION_POINTS_TOP * factor)
