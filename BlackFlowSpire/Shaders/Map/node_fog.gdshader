shader_type canvas_item;

uniform sampler2D cloud_texture : repeat_enable;
uniform vec4 ink_color : source_color = vec4(0.31, 0.27, 0.38, 0.2);

void fragment() {
    vec2 position = UV * 2.0 - 1.0;
    float edge = 1.0 - smoothstep(0.45, 1.0, length(position));
    float cloud = texture(cloud_texture, UV * 1.3 + vec2(TIME * 0.012, TIME * -0.008)).r;
    COLOR = vec4(ink_color.rgb, ink_color.a * edge * (0.55 + cloud * 0.45));
}
