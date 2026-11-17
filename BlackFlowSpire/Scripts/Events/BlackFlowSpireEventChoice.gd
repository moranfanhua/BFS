extends Control

signal choice_confirmed(index: int)
signal leave_pressed

const DESIGN := Vector2(1422.222222, 800)
const OPTION_SCENE := preload("res://BlackFlowSpire/Scenes/Events/BlackFlowSpireEventOption.tscn")
const CHOICE_SPACING := 20.0
const TEXT_INTERVAL := 0.04
const INTRO_BLOCK_TIME := 2.0

var _elapsed := 0.0
var _busy := false
var _selected := -1
var _complete_text := false
var _choice_layout: Dictionary
var _result := false
var _result_elapsed := 0.0
var _expansion: Array[float] = []
var _choice_tweens: Array[Tween] = []

@onready var _choices: Control = $Canvas/Choices
@onready var _description: RichTextLabel = $Canvas/Story/Description
@onready var _leave: Button = $Canvas/Leave

func _ready() -> void:
	_choice_layout = JSON.parse_string(FileAccess.get_file_as_string("res://BlackFlowSpire/Data/Events/exit-choice-layout.json"))
	resized.connect(_layout)
	_leave.pressed.connect(_on_leave_pressed)
	_layout()
	_update_intro()

func _layout() -> void:
	var factor := minf(size.x / DESIGN.x, size.y / DESIGN.y)
	$Canvas.scale = Vector2.ONE * factor
	$Canvas.position = (size - DESIGN * factor) / 2.0

func bind_story(background: Texture2D, title: String, description: String) -> void:
	$Canvas/Artwork.bind_background(background)
	$Canvas/Story/Title.text = title
	_description.text = description

func configure_choices(count: int) -> void:
	if _result:
		_result = false
		_choices.show()
		_leave.hide()
		_select_choice(-1)
	if count == _choices.get_child_count():
		_update_intro()
		return
	for tween in _choice_tweens:
		if tween: tween.kill()
	for child in _choices.get_children():
		_choices.remove_child(child)
		child.queue_free()
	_selected = -1
	_expansion.resize(count)
	_expansion.fill(0.0)
	_choice_tweens.resize(count)
	_choice_tweens.fill(null)
	for index in count:
		var option := OPTION_SCENE.instantiate() as Button
		_choices.add_child(option)
		option.pressed.connect(_on_choice_pressed.bind(index))
		option.get_node("Confirm").pressed.connect(_on_choice_confirmed.bind(index))
	_update_intro()

func get_choice(index: int) -> Button:
	return _choices.get_child(index) as Button if index >= 0 and index < _choices.get_child_count() else null

func bind_choice(index: int, title: String, description: String, icon: Texture2D, enabled: bool = true) -> void:
	var option := get_choice(index)
	option.set_meta("available", enabled)
	option.get_node("Title").text = title
	option.get_node("Description").text = description
	option.get_node("Function").texture = icon
	option.get_node("Title").modulate.a = 1.0 if enabled else 0.35
	option.get_node("Description").modulate.a = 1.0 if enabled else 0.35
	option.get_node("Function").modulate.a = 0.30 if enabled else 0.12
	_update_buttons()

func reset_selection() -> void:
	_select_choice(-1)

func show_result(leave_tooltip: String) -> void:
	_leave.tooltip_text = leave_tooltip
	if _result:
		return
	_result = true
	_result_elapsed = 0.0
	_choices.hide()
	_leave.show()
	_update_intro()

func _on_leave_pressed() -> void:
	if _is_typing():
		_result_elapsed = _description.get_total_character_count() * TEXT_INTERVAL
		_description.visible_characters = -1
		return
	leave_pressed.emit()

func _is_typing() -> bool:
	return _description.visible_characters >= 0 and _description.visible_characters < _description.get_total_character_count()

func _on_choice_pressed(index: int) -> void:
	_select_choice(index)
	get_choice(index).get_node("Confirm").grab_focus()

func _on_choice_confirmed(index: int) -> void:
	if _selected == index:
		choice_confirmed.emit(index)

func _select_choice(index: int) -> void:
	if index == _selected: return
	_selected = index
	for i in _choices.get_child_count():
		var option := get_choice(i)
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
	var count := _choices.get_child_count()
	if count == 0 or _choice_layout.is_empty(): return
	var folded: Array = _choice_layout.folded.size
	var expanded: Array = _choice_layout.expanded.size
	var height := count * float(folded[1]) + (count - 1) * CHOICE_SPACING
	for expansion in _expansion:
		height += expansion * (float(expanded[1]) - float(folded[1]))
	var top := (DESIGN.y / 2.0 - _choices.position.y) / _choices.scale.y - height / 2.0
	for i in count:
		var option := get_choice(i)
		var amount := _expansion[i]
		var dimensions := Vector2(lerpf(folded[0], expanded[0], amount), lerpf(folded[1], expanded[1], amount))
		option.size = dimensions
		option.position = Vector2(_choices.size.x - dimensions.x, top)
		top += dimensions.y + CHOICE_SPACING
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

func _process(delta: float) -> void:
	_elapsed += delta
	if _result: _result_elapsed += delta
	_update_intro()

func _update_intro() -> void:
	# Native scene curves drive the artwork; the separate intro panel uses its
	# serialized 0.6-second slide and 0.04-second typewriter settings.
	var intro := clampf((_elapsed - 1.0) / 0.6, 0.0, 1.0)
	intro = 1.0 - pow(1.0 - intro, 3)
	_choices.position.x = DESIGN.x - _choices.size.x * _choices.scale.x + 650.0 * (1.0 - intro)
	$Canvas/Story.modulate.a = intro
	$Canvas/Story.position.x = 168.0 * (1.0 - intro)
	if _result:
		_description.visible_characters = floori(_result_elapsed / TEXT_INTERVAL)
	else:
		_description.visible_characters = -1 if _complete_text else maxi(0, floori((_elapsed - 1.0) / TEXT_INTERVAL))
		_layout_choices()
	_update_buttons()

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		if _result:
			return
		if _elapsed < INTRO_BLOCK_TIME or _is_typing():
			_complete_text = true
		else:
			_select_choice(-1)
		_update_intro()
		accept_event()

func _update_buttons() -> void:
	_leave.disabled = _busy or _elapsed < INTRO_BLOCK_TIME
	for i in _choices.get_child_count():
		var option := get_choice(i)
		option.disabled = _busy or _result or _elapsed < INTRO_BLOCK_TIME or not option.get_meta("available", true)
		option.get_node("Confirm").disabled = option.disabled
