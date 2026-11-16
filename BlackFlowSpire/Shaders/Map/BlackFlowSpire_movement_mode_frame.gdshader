shader_type canvas_item;
render_mode unshaded;

uniform sampler2D first_flow : filter_linear, repeat_enable;
uniform sampler2D second_flow : filter_linear, repeat_enable;

void fragment() {
	float first = texture(first_flow, UV * vec2(0.73, 1.0) + TIME * vec2(-0.20, -0.10)).r;
	float second = texture(second_flow, UV * vec2(1.52, 1.0) + TIME * vec2(0.11, 0.05)).r;
	COLOR.a *= mix(0.52, 1.0, smoothstep(0.22, 0.78, (first + second) * 0.5));
}
