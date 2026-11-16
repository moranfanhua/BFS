// CanvasItem translation of the extracted UI-Fx-Common sampling operations.
// Unity's original animated UI business code is unavailable; timing is local.
uniform sampler2D base_texture : source_color, repeat_enable;
uniform sampler2D distortion_texture : repeat_enable;
uniform sampler2D dissolve_one_texture : repeat_enable;
uniform sampler2D dissolve_two_texture : repeat_enable;
uniform sampler2D mask_texture : repeat_enable;
uniform bool has_base = false;
uniform bool has_distortion = false;
uniform bool has_dissolve_one = false;
uniform bool has_dissolve_two = false;
uniform bool has_mask = false;
uniform vec4 base_st = vec4(1.0, 1.0, 0.0, 0.0);
uniform vec4 distortion_st = vec4(1.0, 1.0, 0.0, 0.0);
uniform vec4 dissolve_one_st = vec4(1.0, 1.0, 0.0, 0.0);
uniform vec4 dissolve_two_st = vec4(1.0, 1.0, 0.0, 0.0);
uniform vec4 mask_st = vec4(1.0, 1.0, 0.0, 0.0);
uniform vec2 base_speed = vec2(0.0);
uniform vec2 distortion_speed = vec2(0.0);
uniform vec2 distortion_strength = vec2(0.0);
uniform vec4 dissolve_speed = vec4(0.0);
uniform vec2 dissolve_threshold = vec2(0.0);
uniform vec2 dissolve_smoothness = vec2(0.3);
uniform vec2 dissolve_invert = vec2(0.0);
uniform vec2 dissolve_distort = vec2(0.0);
uniform bool distortion_signed = false;
uniform bool base_red_channel = false;
uniform bool clamp_v = false;
uniform bool mask_invert = false;
uniform vec4 tint : source_color = vec4(1.0);
uniform float intensity = 1.0;
uniform bool animate = true;
uniform float preview_time = 3.0;
varying vec4 vertex_tint;

vec4 sample_unity(sampler2D image, vec2 uv) {
    return texture(image, vec2(uv.x, 1.0 - uv.y));
}

float dissolve_value(vec4 sample_color, float threshold, float softness, bool invert_value) {
    float value = sample_color.r * sample_color.a;
    if (invert_value) value = 1.0 - value;
    return smoothstep(threshold, threshold + max(softness, 0.0001), value);
}

void vertex() { vertex_tint = COLOR; }

vec4 shade_uv(vec2 uv, float time) {
    vec2 distorted = uv;
    if (has_distortion) {
        float noise = sample_unity(distortion_texture, uv * distortion_st.xy + distortion_st.zw + distortion_speed * time).r;
        if (distortion_signed) noise = noise * 2.0 - 1.0;
        distorted += noise * distortion_strength;
    }
    vec2 base_uv = distorted * base_st.xy + base_st.zw + base_speed * time;
    if (clamp_v) base_uv.y = clamp(base_uv.y, 0.0, 1.0);
    vec4 result = vec4(1.0);
    if (has_base) result = sample_unity(base_texture, base_uv);
    if (base_red_channel) result = vec4(result.r);
    if (has_dissolve_one) {
        vec2 one_uv = dissolve_distort.x > 0.5 ? distorted : uv;
        vec4 sample_one = sample_unity(dissolve_one_texture, one_uv * dissolve_one_st.xy + dissolve_one_st.zw + dissolve_speed.xy * time);
        result.a *= dissolve_value(sample_one, dissolve_threshold.x, dissolve_smoothness.x, dissolve_invert.x > 0.5);
    }
    if (has_dissolve_two) {
        vec2 two_uv = dissolve_distort.y > 0.5 ? distorted : uv;
        vec4 sample_two = sample_unity(dissolve_two_texture, two_uv * dissolve_two_st.xy + dissolve_two_st.zw + dissolve_speed.zw * time);
        result.a *= dissolve_value(sample_two, dissolve_threshold.y, dissolve_smoothness.y, dissolve_invert.y > 0.5);
    }
    if (has_mask) {
        float mask_alpha = sample_unity(mask_texture, uv * mask_st.xy + mask_st.zw).a;
        if (mask_invert) mask_alpha = 1.0 - mask_alpha;
        result.a *= mask_alpha;
    }
    return result;
}

void fragment() {
    float time = animate ? TIME + preview_time : preview_time;
    vec4 result = shade_uv(UV, time);
    // The radial mesh duplicates its angular seam. Blend the two limits so
    // non-periodic dissolve masks do not produce a hard cut at that seam.
    float angle = fract(UV.x);
    float edge = min(angle, 1.0 - angle);
    if (edge < 0.08) {
        vec2 adjacent_uv = UV + vec2(angle < 0.5 ? 1.0 : -1.0, 0.0);
        result = mix(result, shade_uv(adjacent_uv, time), 0.5 * (1.0 - smoothstep(0.0, 0.08, edge)));
    }
    // Alpha falloff also prevents sampling at the outermost UV row from
    // leaving a one-pixel black circumference after the engine migration.
    result.a *= 1.0 - smoothstep(0.72, 0.99, UV.y);
    result *= tint * vertex_tint * intensity;
    result.a = clamp(result.a, 0.0, 1.0);
    COLOR = result;
}
