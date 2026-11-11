extends Control

signal background_clicked

@export var default_zoom := 1.2
@export var fit_margins := Vector4(72.0,32.0,540.0,40.0)
@export var drag_threshold := 6.0
@export var pan_padding := 48.0

var _zoom := 1.2
var _pan := Vector2.ZERO
var _base_position := Vector2.ZERO
var _scale := 1.0
var _layout_queued := false
var _configured := false
var _interaction_enabled := true
var _armed := false
var _dragging := false
var _press_mouse := Vector2.ZERO
var _press_position := Vector2.ZERO
var _pressed_node: BaseButton
var _disabled_node: BaseButton

func _ready() -> void:
	resized.connect(refresh_camera)
	get_window().focus_exited.connect(_cancel_drag)

func configure_camera(world_size: Vector2, state: Dictionary = {}) -> void:
	$RoadsAndRooms.size=world_size
	_zoom=float(state.get("zoom",default_zoom))
	_pan=state.get("pan",Vector2.ZERO)
	_configured=true
	refresh_camera()

func capture_camera() -> Dictionary:
	return {"zoom":_zoom,"pan":_pan}

func set_interaction_enabled(enabled: bool) -> void:
	_interaction_enabled=enabled
	if not enabled: _cancel_drag()

func refresh_camera() -> void:
	if _layout_queued: return
	_layout_queued=true
	_layout_camera.call_deferred()

func _ui_factor() -> float:
	return clampf(minf(size.x/1920.0,size.y/950.0),0.45,1.25)

func _layout_camera() -> void:
	_layout_queued=false
	if not _configured or not is_inside_tree(): return
	_cancel_drag()
	var factor := _ui_factor()
	var fit_size := size-Vector2(fit_margins.x+fit_margins.z,fit_margins.y+fit_margins.w)*factor
	var world_size := $RoadsAndRooms.size as Vector2
	var fitted := minf(1.2,minf(maxf(1.0,fit_size.x)/world_size.x,maxf(1.0,fit_size.y)/world_size.y))
	_scale=maxf(0.01,fitted*_zoom)
	$RoadsAndRooms.scale=Vector2.ONE*_scale
	_base_position=Vector2(fit_margins.x,fit_margins.y)*factor+(fit_size-world_size*_scale)/2.0
	_apply_pan(_base_position+_pan*_scale)

func _map_area() -> Rect2:
	var area := Rect2(Vector2.ZERO,size)
	var details := get_parent().get_node_or_null("ForestNodeDetails") as Control
	if details and details.is_visible_in_tree():
		var input := details.get_node_or_null("Canvas/PanelInput") as Control
		if input:
			var left := get_global_transform_with_canvas().affine_inverse()*input.get_global_transform_with_canvas().origin
			area.size.x=clampf(left.x,1.0,size.x)
	return area

func _apply_pan(target: Vector2) -> void:
	var area := _map_area()
	var extent: Vector2=$RoadsAndRooms.size*_scale
	var padding := pan_padding*_ui_factor()
	for axis in 2:
		var a := area.position[axis]+padding
		var b := area.end[axis]-extent[axis]-padding
		target[axis]=clampf(target[axis],minf(a,b),maxf(a,b))
	$RoadsAndRooms.position=target
	_pan=(target-_base_position)/_scale

func ensure_node_visible(center: Vector2) -> void:
	if not _configured or _layout_queued: return
	var area := _map_area().grow(-24.0*_ui_factor())
	var target: Vector2=$RoadsAndRooms.position
	var point := target+center*_scale
	var before := point-Vector2(100.0,100.0)*_scale
	var after := point+Vector2(100.0,100.0)*_scale
	for axis in 2:
		if before[axis]<area.position[axis]: target[axis]+=area.position[axis]-before[axis]
		elif after[axis]>area.end[axis]: target[axis]+=area.end[axis]-after[axis]
	_apply_pan(target)

func pan_by(offset: Vector2) -> void:
	if _configured: _apply_pan($RoadsAndRooms.position+offset)

func ensure_keyboard_focus(center: Vector2) -> void:
	if not Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT): ensure_node_visible(center)

func _map_hovered() -> bool:
	if not is_visible_in_tree() or not _configured or not _interaction_enabled: return false
	if not Rect2(Vector2.ZERO,size).has_point(get_local_mouse_position()): return false
	var hovered := get_viewport().gui_get_hovered_control()
	return hovered==self or (hovered!=null and is_ancestor_of(hovered))

func _gui_input(event: InputEvent) -> void:
	map_gui_input(event)

# Arm only after GUI hit testing has delivered the press to the map itself or a
# real node button. UI panels and modals can never start a map drag.
func map_gui_input(event: InputEvent, node: BaseButton = null) -> void:
	if not _interaction_enabled or not _configured or not is_visible_in_tree(): return
	if not event is InputEventMouseButton or not event.pressed or event.button_index!=MOUSE_BUTTON_LEFT: return
	_armed=true
	_dragging=false
	_press_mouse=get_viewport().get_mouse_position()
	_press_position=$RoadsAndRooms.position
	_pressed_node=node

func _input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if _armed and not event.pressed and event.button_index==MOUSE_BUTTON_LEFT:
			var was_dragging := _dragging
			var blank_click := not was_dragging and _pressed_node==null and _map_hovered()
			_armed=false
			_dragging=false
			_pressed_node=null
			mouse_default_cursor_shape=Control.CURSOR_ARROW
			if was_dragging:
				get_viewport().set_input_as_handled()
				_restore_node.call_deferred()
			elif blank_click:
				get_viewport().set_input_as_handled()
				background_clicked.emit()
			return
	elif event is InputEventMouseMotion and _armed:
		if not _dragging and event.position.distance_to(_press_mouse)>=drag_threshold:
			_dragging=true
			# Cancel BaseButton's pending click before its release can select a node.
			if is_instance_valid(_pressed_node) and not _pressed_node.disabled:
				_disabled_node=_pressed_node
				_disabled_node.disabled=true
		if not _dragging: return
		var inverse := get_global_transform_with_canvas().affine_inverse()
		_apply_pan(_press_position+(inverse*event.position-inverse*_press_mouse))
		mouse_default_cursor_shape=Control.CURSOR_DRAG
		get_viewport().set_input_as_handled()

func _restore_node() -> void:
	if is_instance_valid(_disabled_node): _disabled_node.disabled=false
	_disabled_node=null

func _cancel_drag() -> void:
	_armed=false
	_dragging=false
	_pressed_node=null
	mouse_default_cursor_shape=Control.CURSOR_ARROW
	_restore_node()

func _notification(what: int) -> void:
	if what==NOTIFICATION_VISIBILITY_CHANGED and not is_visible_in_tree(): _cancel_drag()

func _exit_tree() -> void:
	_cancel_drag()
