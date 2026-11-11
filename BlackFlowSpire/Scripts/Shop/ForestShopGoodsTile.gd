@tool
extends Button

const NORMAL := preload("res://BlackFlowSpire/Images/Shop/goods-buy.png")
const UNAFFORDABLE := preload("res://BlackFlowSpire/Images/Shop/goods-unaffordable.png")
const SELL := preload("res://BlackFlowSpire/Images/Shop/goods-sell.png")
const USED := preload("res://BlackFlowSpire/Images/Shop/part-use.png")
const EMPTY_USE := preload("res://BlackFlowSpire/Images/Shop/part-use-empty.png")

func _ready() -> void:
	mouse_entered.connect(_highlight.bind(true))
	mouse_exited.connect(_highlight.bind(false))
	focus_entered.connect(_highlight.bind(true))
	focus_exited.connect(_highlight.bind(false))

func bind_item(title: String, price: int, stocked: bool, affordable: bool, sale: bool, selling: bool = false) -> void:
	$Title.text = title
	$Price/Value.text = str(price)
	# The source price icon follows the text's measured width, including 3-digit prices.
	var price_label := $Price/Value as Label
	var price_width := price_label.get_theme_font("font").get_string_size(str(price), HORIZONTAL_ALIGNMENT_LEFT, -1, 43).x
	$Price/Currency.position.x = 185.5 - price_width - 39.0
	$Price/Plus.position.x = $Price/Currency.position.x - 12.0
	$Price/Plus.visible = selling
	$Price.visible = stocked
	$SoldOut.visible = not stocked
	$SoldOutBackground.visible = not stocked
	$Sale.visible = sale and stocked
	$Background.texture = SELL if selling else NORMAL if affordable or not stocked else UNAFFORDABLE
	$Background.modulate = Color.WHITE
	$Artwork.modulate = Color.WHITE if stocked else Color(0.45, 0.45, 0.45, 0.4)
	_highlight(is_hovered() or has_focus())

func bind_part(category: String, uses: int, max_uses: int, loaded: bool) -> void:
	$PartRing.visible = true
	$PartType.visible = true
	$PartRing.texture = load("res://BlackFlowSpire/Images/Shop/part-%s-ring.png" % category)
	$PartType.texture = load("res://BlackFlowSpire/Images/Shop/part-%s-type.png" % category)
	$Artwork.position = Vector2(44, 49)
	$Artwork.size = Vector2(108, 108)
	$PartLoaded.visible = loaded
	for child in $PartUses.get_children():
		$PartUses.remove_child(child)
		child.queue_free()
	for index in max_uses:
		var star := TextureRect.new()
		star.texture = USED if index < uses else EMPTY_USE
		star.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		star.position = Vector2(149 + (index % 2) * 13, 44 + floori(index / 2.0) * 15)
		star.size = Vector2(14, 15.4)
		star.mouse_filter = Control.MOUSE_FILTER_IGNORE
		$PartUses.add_child(star)

func set_selected(selected: bool) -> void:
	$Selected.visible = selected

func _highlight(enabled: bool) -> void:
	# Preserve the original artwork; no custom outline or extra card chrome.
	$Background.modulate = Color(1.12, 1.12, 1.12) if enabled and not disabled else Color.WHITE
