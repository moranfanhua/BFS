extends Control

const Curves = preload("res://BlackFlowSpire/Scripts/Map/ForestMarkerAnimation.gd")
@export var marker_scale := 0.8
var _show: Dictionary
var _loop: Dictionary
var _time := 0.0
var _tip_center: Vector2

func _ready() -> void:
	_show=JSON.parse_string(FileAccess.get_file_as_string("res://BlackFlowSpire/Data/MapMarkers/cursor_show.json"))
	_loop=JSON.parse_string(FileAccess.get_file_as_string("res://BlackFlowSpire/Data/MapMarkers/cursor_loop.json"))
	$Artwork.scale=Vector2.ONE*marker_scale
	var person := $Artwork/panel_cursor/person_ctrl
	for name in ["body_drop","body"]:
		var body := person.get_node("group_body/"+name) as ColorRect
		body.material=body.material.duplicate()
		body.material.set_shader_parameter("dark_body",body.get_meta("dark_body",false))
	_tip_center=$Artwork/panel_cursor/tip_ctrl/you_are_here.position
	seek_marker(0.0)

func _process(delta: float) -> void:
	seek_marker(_time+delta)

func seek_marker(at: float) -> void:
	_time=at
	Curves.apply($Artwork,_show,minf(at,1.5))
	Curves.apply($Artwork/panel_cursor/person_ctrl,_loop,fmod(at,5.0))
	# The source has a separate 2.5s yoyo tween from Unity Y=-4 to Y=4.
	var phase := fmod(at,5.0)/2.5
	var ratio := phase if phase<=1.0 else 2.0-phase
	var offset := lerpf(-4.0,4.0,sin(ratio*PI/2.0))
	$Artwork/panel_cursor/tip_ctrl/you_are_here.position=_tip_center+Vector2(0.0,-offset)

func freeze_preview(at: float) -> void:
	set_process(false)
	seek_marker(at)
	for name in ["body_drop","body"]:
		var body := get_node("Artwork/panel_cursor/person_ctrl/group_body/"+name) as ColorRect
		body.material.set_shader_parameter("animate",false)
		body.material.set_shader_parameter("preview_time",at)
