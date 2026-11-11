shader_type canvas_item;
render_mode unshaded, blend_mix;

// AnimSheet's UIColor mode: vertex red selects a clamped frame, while vertex
// alpha controls opacity. Rows begin at the top of the extracted PNG sheet.
uniform sampler2D sheet_texture : source_color, filter_linear;
uniform vec2 grid = vec2(4.0);
uniform vec4 control_color = vec4(1.0);
uniform vec4 tint : source_color = vec4(1.0);
uniform float intensity = 1.0;
varying vec4 inherited_tint;

void vertex() { inherited_tint = COLOR; }
void fragment() {
	float count = grid.x * grid.y;
	float frame = min(count - 1.0, floor(clamp(control_color.r, 0.0, 1.0) * count));
	vec2 cell = vec2(mod(frame, grid.x), floor(frame / grid.x));
	vec4 sample_color = texture(sheet_texture, (UV + cell) / grid);
	COLOR = sample_color * tint * inherited_tint * vec4(vec3(intensity), control_color.a);
}
