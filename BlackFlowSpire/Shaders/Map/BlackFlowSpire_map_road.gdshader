shader_type canvas_item;
render_mode unshaded, blend_mix;

// LavaLiquid2D: RGB channels scroll separately before base/inner-light thresholds.
// The original PNGs contain control channels, not the final road color.
uniform sampler2D shape_texture : repeat_enable, filter_linear;
uniform sampler2D dissolve_a : repeat_enable, filter_linear;
uniform sampler2D dissolve_b : repeat_enable, filter_linear;
uniform bool passable = false;
uniform bool animate = true;
uniform float preview_time = 3.0;
uniform float flow_phase = 0.0;
uniform float grow_b = 0.43921569;

vec4 unity_sample(sampler2D image, vec2 uv) {
    return texture(image, vec2(uv.x, 1.0-uv.y));
}

void fragment() {
    // Godot's UV origin is top-left; material parameters use Unity's bottom-left.
    vec2 uv = vec2(UV.x, 1.0-UV.y);
    float time = (animate ? TIME : preview_time) + flow_phase;
    vec3 speeds = passable ? vec3(-0.1,0.07,0.15) : vec3(-0.05,0.035,0.05);
    float value = (
        unity_sample(shape_texture,uv+vec2(0.0,time*speeds.r)).r +
        unity_sample(shape_texture,uv+vec2(0.0,time*speeds.g)).g +
        unity_sample(shape_texture,uv+vec2(0.0,time*speeds.b)).b
    ) / 3.0;
    float base_step = passable ? 0.254 : 0.397;
    float base_softness = passable ? 0.13 : 0.24;
    float inner_step = passable ? 1.04 : 0.76;
    float inner_softness = passable ? 1.08 : 0.46;
    float base = smoothstep(base_step,base_step+base_softness,value);
    float inner = smoothstep(0.0,1.0,(value-inner_step)/-inner_softness);
    float gray = (passable ? 0.01960784 : 0.128) + inner*(passable ? 0.73 : 0.3773585);
    float alpha = base;
    alpha *= smoothstep(0.04313731,0.04313731+0.33,unity_sample(dissolve_a,uv).r);
    float threshold = grow_b*2.0-1.0;
    alpha *= smoothstep(threshold,threshold+0.25,1.0-unity_sample(dissolve_b,uv).r);
    alpha *= passable ? 1.0 : 0.89411765;
    COLOR = vec4(vec3(base*gray),alpha);
}
