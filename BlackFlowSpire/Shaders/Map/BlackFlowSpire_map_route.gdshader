shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform sampler2D dissolve_texture : repeat_disable, filter_linear;
uniform float threshold = 1.0;
uniform float opacity = 0.0;

void fragment() {
    vec4 image = texture(TEXTURE,UV);
    float mask = texture(dissolve_texture,UV).r;
    image.a *= smoothstep(threshold,threshold+0.447,mask)*opacity;
    COLOR = image;
}
