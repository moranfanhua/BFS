shader_type canvas_item;
render_mode unshaded;

uniform sampler2D result_image : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float brightness = 0.4;

void fragment() {
    COLOR = vec4(textureLod(result_image, SCREEN_UV, 4.0).rgb * brightness, 1.0);
}
