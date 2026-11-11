extends Control

const Curves = preload("res://BlackFlowSpire/Scripts/Map/ForestMarkerAnimation.gd")
@export var marker_scale := 0.65
var _show: Dictionary
var _hide: Dictionary
var _time := 0.0
var _active := false
var _empty := false
var _outgoing: Array[Dictionary] = []

func _ready() -> void:
	_show=JSON.parse_string(FileAccess.get_file_as_string("res://BlackFlowSpire/Data/MapMarkers/focus_show.json"))
	_hide=JSON.parse_string(FileAccess.get_file_as_string("res://BlackFlowSpire/Data/MapMarkers/focus_hide.json"))
	set_process(false)

func select_node(at: Vector2, empty: bool) -> void:
	if _active and position.is_equal_approx(at) and _empty==empty: return
	if _active: _fade_previous()
	position=at
	_empty=empty
	_time=0.0
	_active=true
	$Artwork.show()
	$Artwork/variant_empty.visible=empty
	$Artwork/variant_normal.visible=not empty
	_apply($Artwork,_show,0.0)
	set_process(true)

func clear_selection() -> void:
	if not _active: return
	_fade_previous()
	_active=false
	$Artwork.hide()
	set_process(true)

# A fading previous target remains at its old world position during quick clicks.
# Only the new target is active; these temporary copies cannot receive input.
func _fade_previous() -> void:
	var previous := $Artwork.duplicate() as Node2D
	previous.position=position
	get_parent().add_child(previous)
	get_parent().move_child(self,get_parent().get_child_count()-1)
	var current := get_parent().get_node_or_null("CurrentPositionMarker")
	if current: get_parent().move_child(current,get_parent().get_child_count()-1)
	_outgoing.append({"node":previous,"time":0.0,"alpha":$Artwork.modulate.a,"scale":$Artwork/variant_normal.scale})

func _process(delta: float) -> void:
	if _active:
		_time=minf(_time+delta,0.5)
		_apply($Artwork,_show,_time)
	for index in range(_outgoing.size()-1,-1,-1):
		var outgoing: Dictionary = _outgoing[index]
		outgoing.time+=delta
		_apply(outgoing.node,_hide,outgoing.time)
		outgoing.node.modulate.a*=outgoing.alpha
		outgoing.node.get_node("variant_normal").scale*=outgoing.scale
		outgoing.node.get_node("variant_empty").scale*=outgoing.scale
		if outgoing.time>=0.33333334:
			outgoing.node.queue_free()
			_outgoing.remove_at(index)
	if (not _active or _time>=0.5) and _outgoing.is_empty(): set_process(false)

func _apply(art: Node2D, clip: Dictionary, at: float) -> void:
	Curves.apply(art,clip,at)
	art.scale=Vector2.ONE*marker_scale

func freeze_preview(at: float = 0.5) -> void:
	set_process(false)
	_time=at
	for outgoing in _outgoing: outgoing.node.queue_free()
	_outgoing.clear()
	if _active: _apply($Artwork,_show,at)
