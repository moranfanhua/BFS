shader_type canvas_item;
render_mode unshaded, blend_mix;

// Extracted cursor_body_lava material; vertex tint remains display color because
// this material has RGB control disabled (unlike the map road material).
uniform sampler2D lava_texture : repeat_enable, filter_linear;
uniform sampler2D body_mask : repeat_enable, filter_linear;
uniform bool dark_body = false;
uniform bool animate = true;
uniform float preview_time = 0.0;
varying vec4 vertex_tint;

void vertex() { vertex_tint=COLOR; }

void fragment() {
    float time=animate ? TIME : preview_time;
    vec2 uv=UV;
    float value=(texture(lava_texture,uv).r+texture(lava_texture,uv+vec2(0.0,0.06*time)).g+texture(lava_texture,uv+vec2(0.0,0.02*time)).b)/3.0;
    float base=smoothstep(0.204,0.364,value);
    float inner=smoothstep(0.0,1.0,(value-0.92)/-1.91);
    float alpha=base*smoothstep(0.19,0.765,texture(body_mask,uv).r);
    COLOR=vec4(vec3(dark_body ? 0.0 : base*(1.0+inner)),alpha)*vertex_tint;
}
