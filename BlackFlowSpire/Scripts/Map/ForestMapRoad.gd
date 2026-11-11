extends ColorRect

var _grow_tween: Tween
var _route_tween: Tween
var _selected := false

func _ready() -> void:
	material = material.duplicate()
	$Route.material = $Route.material.duplicate()
	material.set_shader_parameter("grow_b",1.0)
	_grow_tween = create_tween()
	_grow_tween.tween_method(_grow,0.0,0.5,0.5)

func configure(is_passable: bool, shape_index: int, phase: float) -> void:
	set_passable(is_passable)
	material.set_shader_parameter("flow_phase",phase)
	material.set_shader_parameter("shape_texture",load("res://BlackFlowSpire/Images/Map/Roads/lava_line_%d.png" % shape_index))

func set_passable(is_passable: bool) -> void:
	material.set_shader_parameter("passable",is_passable)

# Original blue control-channel keys and Hermite slopes from grid_zone_line_grow_anim.
func _grow(time: float) -> void:
	var a := Vector3(0.0,1.0,0.0)
	var b := Vector3(0.16666667,0.87577337,-0.99381304)
	var c := Vector3(0.5,0.43921569,0.0)
	if time > b.x: a=b; b=c
	var duration := b.x-a.x
	var f := clampf((time-a.x)/duration,0.0,1.0)
	var value := (2*f*f*f-3*f*f+1)*a.y+(f*f*f-2*f*f+f)*duration*a.z+(-2*f*f*f+3*f*f)*b.y+(f*f*f-f*f)*duration*b.z
	material.set_shader_parameter("grow_b",value)

func set_selected(selected: bool) -> void:
	if selected == _selected: return
	_selected = selected
	if _route_tween: _route_tween.kill()
	_route_tween = create_tween().set_parallel(true)
	_route_tween.tween_property($Route.material,"shader_parameter/opacity",1.0 if selected else 0.0,0.15 if selected else 0.2)
	_route_tween.tween_property($Route.material,"shader_parameter/threshold",0.348 if selected else 1.0,0.5 if selected else 0.2).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)

func freeze_preview(time: float, selected: bool) -> void:
	if _grow_tween: _grow_tween.kill()
	if _route_tween: _route_tween.kill()
	material.set_shader_parameter("animate",false)
	material.set_shader_parameter("preview_time",time)
	material.set_shader_parameter("grow_b",0.43921569)
	$Route.material.set_shader_parameter("opacity",1.0 if selected else 0.0)
	$Route.material.set_shader_parameter("threshold",0.348)
