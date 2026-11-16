extends RefCounted

# Original unweighted Unity curves, including vector/quaternion keys and tangents.
static func sample(track: Dictionary, at: float) -> Variant:
	var keys: Array = track["keys"]
	if at <= float(keys[0].time): return keys[0].value
	if at >= float(keys[-1].time): return keys[-1].value
	for index in range(keys.size()-1):
		var a: Dictionary = keys[index]
		var b: Dictionary = keys[index+1]
		if at > float(b.time): continue
		if track.attribute == "m_IsActive": return a.value
		var duration := float(b.time)-float(a.time)
		var ratio := (at-float(a.time))/duration
		if a.value is Dictionary:
			var result := {}
			for axis in a.value:
				result[axis] = hermite(a.value[axis],b.value[axis],a.outSlope[axis],b.inSlope[axis],ratio,duration)
			return result
		return hermite(a.value,b.value,a.outSlope,b.inSlope,ratio,duration)
	return keys[-1].value

static func hermite(a: float, b: float, out_slope: float, in_slope: float, ratio: float, duration: float) -> float:
	return (2*ratio*ratio*ratio-3*ratio*ratio+1)*a+(ratio*ratio*ratio-2*ratio*ratio+ratio)*duration*out_slope+(-2*ratio*ratio*ratio+3*ratio*ratio)*b+(ratio*ratio*ratio-ratio*ratio)*duration*in_slope

static func apply(root: Node2D, clip: Dictionary, at: float) -> void:
	for track in clip.tracks:
		var node: Variant = root.get_node_or_null(NodePath(track.path if track.path!="" else "."))
		if node == null: continue
		var value: Variant = sample(track,at)
		var attribute: String = track.attribute
		if attribute == "m_Alpha": node.modulate.a=clampf(float(value),0.0,1.0)
		elif attribute == "m_ScaleCurves": node.scale=Vector2(value.x,value.y)
		elif attribute == "m_RotationCurves": node.rotation=-2.0*atan2(value.z,value.w)
		elif attribute == "m_AnchoredPosition.y":
			node.position.y=-float(value)-(node.size.y/2.0 if node is Control else 0.0)
		elif attribute.begins_with("m_SizeDelta") and node is Control:
			var center: Vector2 = node.position+node.size/2.0
			if attribute.ends_with("x"): node.size.x=maxf(0.0,float(value))
			else: node.size.y=maxf(0.0,float(value))
			node.position=center-node.size/2.0
			node.pivot_offset=node.size/2.0
