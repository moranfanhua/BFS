extends Button

const STAR := preload("res://BlackFlowSpire/Images/Map/Movement/move_mode_icon_times.png")
const EMPTY_STAR := preload("res://BlackFlowSpire/Images/Map/Movement/move_mode_icon_times_empty.png")
const STAR_POSITIONS := [Vector2(401, 13), Vector2(411, 21), Vector2(415, 37), Vector2(413, 54), Vector2(412, 72)]

func bind_mode(title: String, description: String, icon: String, uses: int, maximum: int, remaining: String, equipped: String) -> void:
	var is_part := not icon.is_empty()
	$Walk.visible = not is_part
	$PartRing.visible = is_part
	$PartArt.visible = is_part
	$Remaining.visible = is_part
	$Name.text = title
	$Description.text = description
	$Description.offset_top = 43.0 if is_part else 13.0
	$Badge/Text.text = equipped
	tooltip_text = title
	if not is_part: return
	$PartArt.texture = load("res://BlackFlowSpire/Images/Parts/%s.png" % icon)
	var count := $Remaining/Count as Label
	count.text = remaining
	$Remaining/Pill.size.x = count.get_minimum_size().x + 65.0
	for index in mini(maximum, STAR_POSITIONS.size()):
		var star := TextureRect.new()
		star.texture = STAR if index < uses else EMPTY_STAR
		star.position = STAR_POSITIONS[index]
		star.mouse_filter = Control.MOUSE_FILTER_IGNORE
		$Stars.add_child(star)

func mark_selected(selected: bool) -> void:
	$Selected.visible = selected
	$Badge.visible = selected
