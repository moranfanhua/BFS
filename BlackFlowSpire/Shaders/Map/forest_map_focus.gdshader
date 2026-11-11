shader_type canvas_item;
render_mode unshaded, blend_mix;
uniform vec4 tint : source_color = vec4(0.86,0.94,0.92,1.0);
uniform float ring_width = 0.018;
uniform float strength = 1.0;
varying vec4 vertex_tint;

void vertex() { vertex_tint = COLOR; }

void fragment() {
    float radius = length(UV-vec2(0.5))*2.0;
    float edge = abs(radius-0.73);
    float stroke = 1.0-smoothstep(ring_width,ring_width+0.012,edge);
    float glow = exp(-edge*edge/0.014)*0.18;
    COLOR = vec4(tint.rgb,(stroke+glow)*tint.a*strength)*vertex_tint;
}
