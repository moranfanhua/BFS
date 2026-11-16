extends Control

const DESIGN := Vector2(1422.222222,800)
var _elapsed := 0.0
var _busy := false
var _selected := -1
var _complete_text := false
var _choice_layout: Dictionary
var _choice_count := 2
var _rest_event := false
var _result := false
var _result_elapsed := 0.0
var _expansion: Array[float] = [0.0, 0.0]
var _choice_tweens: Array[Tween] = [null, null]

func _ready() -> void:
	_choice_layout = JSON.parse_string(FileAccess.get_file_as_string("res://BlackFlowSpire/Data/Events/exit-choice-layout.json"))
	resized.connect(_layout)
	_layout()
	set_busy(false)

func _layout() -> void:
	var factor := minf(size.x / DESIGN.x, size.y / DESIGN.y)
	$Canvas.scale = Vector2.ONE * factor
	$Canvas.position = (size - DESIGN * factor) / 2.0

func bind_event(background: String, title: String, description: String, accept_title: String, accept_description: String, leave_title: String, leave_description: String, final: bool) -> void:
	bind_story(background, title, description)
	$Canvas/Choices/Option0/Title.text = accept_title
	$Canvas/Choices/Option0/Description.text = accept_description
	$Canvas/Choices/Option1/Title.text = leave_title
	$Canvas/Choices/Option1/Description.text = leave_description
	$Canvas/Choices/Option0/Function.texture = load("res://BlackFlowSpire/Images/Events/Exit/%s.png" % ("zone_end" if final else "scrap_drop"))

func bind_story(background: String, title: String, description: String) -> void:
	$Canvas/Artwork.bind_background(background)
	$Canvas/Story/Title.text = title
	$Canvas/Story/Description.text = description

func configure_rest_choices(count: int) -> void:
	_rest_event = true
	_choice_count = count
	for i in range($Canvas/Choices.get_child_count(), count):
		var option := $Canvas/Choices/Option0.duplicate() as Button
		option.name = "Option%s" % i
		$Canvas/Choices.add_child(option)
		_expansion.append(0.0)
		_choice_tweens.append(null)
	_update_intro()

func bind_choice(index: int, title: String, description: String, icon: String) -> void:
	var option := get_node("Canvas/Choices/Option%s" % index)
	option.get_node("Title").text = title
	option.get_node("Description").text = description
	option.get_node("Function").texture = load("res://BlackFlowSpire/Images/Events/Exit/%s.png" % icon)

func show_rest_result() -> void:
	_result = true
	_result_elapsed = 0.0
	$Canvas/Choices.hide()
	$Canvas/Leave.show()
	_update_intro()

func finish_result_text() -> bool:
	var description := $Canvas/Story/Description as RichTextLabel
	if description.visible_characters < 0 or description.visible_characters >= description.get_total_character_count():
		return false
	_result_elapsed = description.get_total_character_count() * 0.04
	description.visible_characters = -1
	return true

func select_choice(index: int) -> void:
	if index == _selected: return
	_selected = index
	for i in _choice_count:
		var option := get_node("Canvas/Choices/Option%s" % i)
		option.get_node("Confirm").visible = i == index
		if _choice_tweens[i]: _choice_tweens[i].kill()
		_choice_tweens[i] = create_tween().set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
		_choice_tweens[i].tween_method(_set_expansion.bind(i), _expansion[i], 1.0 if i == index else 0.0, 0.25)

func _set_expansion(value: float, index: int) -> void:
	_expansion[index] = value
	_layout_choices()

func _choice_rect(key: String, amount: float) -> Rect2:
	var a: Array = _choice_layout.folded[key]
	var b: Array = _choice_layout.expanded[key]
	return Rect2(Vector2(lerpf(a[0], b[0], amount), lerpf(a[1], b[1], amount)), Vector2(lerpf(a[2], b[2], amount), lerpf(a[3], b[3], amount)))

func _set_rect(control: Control, rect: Rect2) -> void:
	control.position = rect.position
	control.size = rect.size

func _layout_choices() -> void:
	if _choice_layout.is_empty(): return
	var top := 0.0
	if _rest_event:
		var height := _choice_count * float(_choice_layout.folded.size[1]) + (_choice_count - 1) * 20.0
		for expansion in _expansion:
			height += expansion * (float(_choice_layout.expanded.size[1]) - float(_choice_layout.folded.size[1]))
		top = (DESIGN.y / 2.0 - $Canvas/Choices.position.y) / $Canvas/Choices.scale.y - height / 2.0
	for i in _choice_count:
		var option := get_node("Canvas/Choices/Option%s" % i) as Button
		var amount := _expansion[i]
		var folded: Array = _choice_layout.folded.size
		var expanded: Array = _choice_layout.expanded.size
		var dimensions := Vector2(lerpf(folded[0], expanded[0], amount), lerpf(folded[1], expanded[1], amount))
		option.size = dimensions
		option.position = Vector2($Canvas/Choices.size.x - dimensions.x, top)
		top += dimensions.y + 20.0
		for item in [["Background", "fold"], ["ExpandedBackground", "expand"], ["Header", "header"], ["Title", "title"], ["Confirm", "confirm"]]:
			_set_rect(option.get_node(item[0]), _choice_rect(item[1], amount))
		option.get_node("Background").modulate.a = 1.0 - amount
		option.get_node("ExpandedBackground").modulate.a = amount
		option.get_node("Confirm").modulate.a = amount
		var glow := _choice_rect("glow", amount)
		glow.position -= _choice_rect("confirm", amount).position
		_set_rect(option.get_node("Confirm/Glow"), glow)
		# Preserve the function glyph's aspect and its reference-screen visual
		# size inside the native 120-pixel holder, instead of filling the holder.
		var function_rect := _choice_rect("function", amount)
		_set_rect(option.get_node("Function"), Rect2(function_rect.get_center() - Vector2(36,36), Vector2(72,72)))
		var description := option.get_node("Description") as RichTextLabel
		var content := _choice_rect("content", amount)
		description.size.x = content.size.x
		var text_height := maxf(1.0, description.get_content_height())
		# Native content has ten pixels of top padding and middle-left child
		# alignment, not a fixed text position at the top of the black plate.
		description.position = content.position + Vector2(0, 10.0 + maxf(0.0, (content.size.y - 10.0 - text_height) / 2.0))
		description.size.y = text_height

func set_busy(busy: bool) -> void:
	_busy = busy
	_update_buttons()

func finish_entry() -> void:
	_elapsed = 5.25
	_complete_text = true
	$Canvas/Artwork.seek_entry(4.65)
	_update_intro()

func _process(delta: float) -> void:
	_elapsed += delta
	if _result: _result_elapsed += delta
	_update_intro()

func _update_intro() -> void:
	# Native scene curves drive the artwork; the separate intro panel uses its
	# serialized 0.6-second slide and 0.04-second typewriter settings.
	var intro := clampf((_elapsed - 1.0) / 0.6, 0.0, 1.0)
	intro = 1.0 - pow(1.0 - intro, 3)
	$Canvas/Choices.position.x = DESIGN.x - $Canvas/Choices.size.x * $Canvas/Choices.scale.x + 650.0 * (1.0 - intro)
	$Canvas/Story.modulate.a = intro
	$Canvas/Story.position.x = 168.0 * (1.0 - intro)
	if _result:
		$Canvas/Story/Description.visible_characters = floori(_result_elapsed / 0.04)
	else:
		$Canvas/Story/Description.visible_characters = -1 if _complete_text else maxi(0, floori((_elapsed - 1.0) / 0.04))
		_layout_choices()
	_update_buttons()

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var description := $Canvas/Story/Description as RichTextLabel
		if not _rest_event:
			finish_entry()
		elif _elapsed < 2.0 or (description.visible_characters >= 0 and description.visible_characters < description.get_total_character_count()):
			_complete_text = true
		else:
			select_choice(-1)
		accept_event()

func _update_buttons() -> void:
	$Canvas/Leave.disabled = _busy or _elapsed < 2.0
	for i in _choice_count:
		var option := get_node("Canvas/Choices/Option%s" % i) as Button
		option.disabled = _busy or _result or _elapsed < 2.0
		option.get_node("Confirm").disabled = option.disabled
