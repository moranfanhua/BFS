shader_type canvas_item;

varying vec4 tint;

void vertex() {
	tint = COLOR;
}

void fragment() {
	vec4 artwork = texture(TEXTURE, UV);
	float value = max(max(artwork.r, artwork.g), artwork.b);
	COLOR = vec4(vec3(pow(value, 0.8)), artwork.a) * tint;
}
