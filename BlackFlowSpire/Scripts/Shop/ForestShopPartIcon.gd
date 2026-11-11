extends Control

const USED := preload("res://BlackFlowSpire/Images/Shop/part-use.png")
const EMPTY_USE := preload("res://BlackFlowSpire/Images/Shop/part-use-empty.png")

func bind_part(icon_id: String, category: String, uses: int, max_uses: int, loaded: bool) -> void:
	$Ring.texture = load("res://BlackFlowSpire/Images/Shop/part-%s-ring.png" % category)
	$Type.texture = load("res://BlackFlowSpire/Images/Shop/part-%s-type.png" % category)
	$Artwork.texture = load("res://BlackFlowSpire/Images/Parts/%s.png" % icon_id)
	$Loaded.visible = loaded
	for child in $Uses.get_children():
		$Uses.remove_child(child)
		child.queue_free()
	for index in max_uses:
		var star := TextureRect.new()
		star.texture = USED if index < uses else EMPTY_USE
		star.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		star.position = Vector2(137 + (index % 2) * 15, floori(index / 2.0) * 18)
		star.size = Vector2(17, 18.7)
		star.mouse_filter = Control.MOUSE_FILTER_IGNORE
		$Uses.add_child(star)
