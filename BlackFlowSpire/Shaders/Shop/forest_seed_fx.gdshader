shader_type canvas_item;
render_mode unshaded;

// Local equivalents for the extracted UI-Fx-Common color-mask, distortion and
// dissolve settings. Source images stay unchanged; the UI clock drives motion.
uniform bool rgb_controls = false;
uniform vec4 tint : source_color = vec4(1.0);
uniform bool distort = false;
uniform sampler2D noise_texture : repeat_enable, filter_linear;
uniform vec2 noise_scale = vec2(1.0);
uniform vec2 noise_offset = vec2(0.0);
uniform vec2 noise_velocity = vec2(0.0);
uniform vec2 noise_amount = vec2(0.0);
uniform bool dissolve = false;
uniform sampler2D dissolve_texture : repeat_enable, filter_linear;
uniform vec2 dissolve_scale = vec2(1.0);
uniform vec2 dissolve_offset = vec2(0.0);
uniform float threshold = -1.0;
uniform float softness = 0.3;
uniform float elapsed = 0.0;
varying vec4 vertex_color;

void vertex() {
    vertex_color = COLOR;
}

void fragment() {
    vec2 uv = UV;
    if (distort) {
        vec2 flow = texture(noise_texture, UV * noise_scale + noise_offset + elapsed * noise_velocity).rg;
        uv += (flow - vec2(0.5)) * noise_amount;
    }
    vec4 source = texture(TEXTURE, clamp(uv, vec2(0.0), vec2(1.0)));
    // _RgbCtrl reserves vertex RGB for effect controls instead of tinting the
    // sprite. In particular the white seed bubble must not inherit its control RGB.
    vec3 rgb = source.rgb;
    float alpha = source.a;
    if (dissolve) {
        float mask = texture(dissolve_texture, UV * dissolve_scale + dissolve_offset).r;
        alpha *= smoothstep(threshold, threshold + max(0.001, softness), mask);
    }
    COLOR = vec4(rgb, alpha) * vec4(rgb_controls ? vec3(1.0) : vertex_color.rgb, vertex_color.a) * tint;
}
