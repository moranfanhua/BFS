shader_type canvas_item;
render_mode unshaded;

varying vec4 glow_tint;

void vertex() {
    glow_tint = COLOR;
}

void fragment() {
    COLOR = vec4(glow_tint.rgb, texture(TEXTURE, UV).a * glow_tint.a);
}
