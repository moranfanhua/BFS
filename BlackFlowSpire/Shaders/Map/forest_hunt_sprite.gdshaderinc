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
uniform vec4 base_st = vec4(1, 1, 0, 0);
uniform vec4 distortion_st = vec4(1, 1, 0, 0);
uniform vec4 dissolve_one_st = vec4(1, 1, 0, 0);
uniform vec4 dissolve_two_st = vec4(1, 1, 0, 0);
uniform vec4 mask_st = vec4(1, 1, 0, 0);
uniform vec2 base_speed = vec2(0);
uniform vec2 distortion_speed = vec2(0);
uniform vec2 distortion_strength = vec2(0);
uniform vec4 dissolve_speed = vec4(0);
uniform vec2 dissolve_threshold = vec2(0);
uniform vec2 dissolve_smoothness = vec2(0.3);
uniform vec2 dissolve_invert = vec2(0);
uniform vec2 dissolve_distort = vec2(0);
uniform bool distortion_signed = false;
uniform bool base_red_channel = false;
uniform bool clamp_v = false;
uniform bool mask_invert = false;
uniform vec4 tint : source_color = vec4(1);
uniform float intensity = 1;
uniform vec4 control_color = vec4(1);
uniform bool controls_as_parameters = false;
uniform bool animate = true;
uniform float preview_time = 0;
uniform float fill_amount : hint_range(0, 1) = 1;
uniform int fill_method = 0;
uniform int fill_origin = 0;
uniform bool fill_clockwise = true;
varying vec4 inherited_tint;
vec4 unity_sample(sampler2D tex, vec2 uv) { return texture(tex, vec2(uv.x, 1.0-uv.y)); }
float fill_progress(vec2 uv) {
	if (fill_method == 0) return fill_origin == 0 ? uv.x : 1.0 - uv.x;
	if (fill_method == 1) return fill_origin == 0 ? 1.0 - uv.y : uv.y;
	vec2 center = vec2(0.5);
	if (fill_method == 3) {
		if (fill_origin == 0) center.y = 1.0;
		if (fill_origin == 1) center.x = 0.0;
		if (fill_origin == 2) center.y = 0.0;
		if (fill_origin == 3) center.x = 1.0;
	}
	float turn = atan(uv.y - center.y, uv.x - center.x) / TAU;
	float start = float(fill_origin) * 0.25 + 0.25;
	float arc = fill_method == 3 ? 0.5 : 1.0;
	if (fill_method == 3) start += fill_clockwise ? 0.25 : -0.25;
	return fract(fill_clockwise ? turn - start : start - turn) / arc;
}
void vertex() { inherited_tint = COLOR; }
void fragment() {
	float time = animate ? TIME + preview_time : preview_time;
	vec2 uv = vec2(UV.x, 1.0-UV.y);
	vec2 distorted = uv;
	if (has_distortion) {
		float noise = unity_sample(distortion_texture, uv*distortion_st.xy + distortion_st.zw + distortion_speed*time).r;
		if (distortion_signed) noise = noise*2.0-1.0;
		distorted += noise*distortion_strength*(controls_as_parameters ? control_color.r : 1.0);
	}
	vec2 base_uv = distorted*base_st.xy + base_st.zw + base_speed*time;
	if (clamp_v) base_uv.y = clamp(base_uv.y, 0.0, 1.0);
	vec4 result = has_base ? unity_sample(base_texture, base_uv) : texture(TEXTURE, vec2(distorted.x, 1.0-distorted.y));
	if (base_red_channel) result = vec4(result.r);
	vec2 thresholds = controls_as_parameters ? control_color.gb*2.0-1.0 : dissolve_threshold;
	if (has_dissolve_one) {
		vec4 sample_color = unity_sample(dissolve_one_texture, (dissolve_distort.x > 0.5 ? distorted : uv)*dissolve_one_st.xy + dissolve_one_st.zw + dissolve_speed.xy*time);
		float value = sample_color.r*sample_color.a;
		if (dissolve_invert.x > 0.5) value = 1.0-value;
		result.a *= smoothstep(thresholds.x, thresholds.x + max(0.0001, dissolve_smoothness.x), value);
	}
	if (has_dissolve_two) {
		vec4 sample_color = unity_sample(dissolve_two_texture, (dissolve_distort.y > 0.5 ? distorted : uv)*dissolve_two_st.xy + dissolve_two_st.zw + dissolve_speed.zw*time);
		float value = sample_color.r*sample_color.a;
		if (dissolve_invert.y > 0.5) value = 1.0-value;
		result.a *= smoothstep(thresholds.y, thresholds.y + max(0.0001, dissolve_smoothness.y), value);
	}
	if (has_mask) {
		float mask_alpha = unity_sample(mask_texture, uv*mask_st.xy + mask_st.zw).a;
		result.a *= mask_invert ? 1.0-mask_alpha : mask_alpha;
	}
	result.rgb *= controls_as_parameters ? vec3(1) : control_color.rgb;
	result.a *= control_color.a;
	if (fill_amount < 1.0 && fill_progress(UV) >= fill_amount) result.a = 0.0;
	COLOR = result*tint*inherited_tint*intensity;
}
