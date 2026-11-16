shader_type canvas_item;
render_mode unshaded;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform float blur_amount = 3.5;
uniform float darkness = 0.4;
uniform float strength = 1;
void fragment() {
	vec3 background = textureLod(screen_texture, SCREEN_UV, blur_amount).rgb * darkness;
	vec2 p = UV*2.0-1.0;
	float vignette = smoothstep(0.2, 1.2, length(p*vec2(0.8, 0.7)));
	background = mix(background, vec3(0.17, 0.006, 0.012), vignette*0.8);
	COLOR = vec4(background, strength);
}
