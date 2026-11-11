extends Control

const STAR := preload("res://BlackFlowSpire/Images/Map/Movement/move_mode_icon_times.png")
const EMPTY_STAR := preload("res://BlackFlowSpire/Images/Map/Movement/move_mode_icon_times_empty.png")

func bind_movement(icon_id: String, remaining: int, maximum: int) -> void:
	var loaded := not icon_id.is_empty()
	$WalkGlow.visible = not loaded
	$WalkIcon.visible = not loaded
	$PartGlow.visible = loaded
	$LoadedPart.visible = loaded
	$Change.visible = loaded
	if not loaded: return
	$LoadedPart/Artwork.texture = load("res://BlackFlowSpire/Images/Parts/%s.png" % icon_id)
	var stars := $LoadedPart/Stars.get_children()
	for index in stars.size():
		stars[index].visible = index < maximum
		stars[index].texture = STAR if index < remaining else EMPTY_STAR
