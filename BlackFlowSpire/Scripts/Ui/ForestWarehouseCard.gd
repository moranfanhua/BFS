extends Button

func bind_part(title: String, description: String, icon: String, category: String, uses: int, maximum: int, loaded: bool, value: int) -> void:
	$Content.show()
	$Empty.hide()
	$Content/Name.text = title
	$Content/Description.text = description
	$Content/Price/Amount.text = str(value)
	$Content/Icon.bind_part(icon, category, uses, maximum, false)
	$Content/Type.texture = $Content/Icon/Type.texture
	$Content/Icon/Type.hide()
	$Frame/Background.modulate.a = 1.0
	$Equipped.visible = loaded
	$Loaded.visible = loaded
	disabled = false

func bind_empty() -> void:
	$Content.hide()
	$Empty.show()
	$Frame/Background.modulate.a = 0.5
	$Equipped.hide()
	$Loaded.hide()
	disabled = true
