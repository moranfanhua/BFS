shader_type canvas_item;
render_mode unshaded, blend_add;
uniform bool animate = true;
uniform float preview_time = 0;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
void fragment() {
	float time = animate ? TIME+preview_time : preview_time;
	vec2 p = UV*vec2(1280,720);
	vec3 sum = vec3(0);
	for (int i=0;i<65;i++) {
		float seed = float(i);
		float speed = mix(3.0,14.0,hash(vec2(seed,1)));
		vec2 center = vec2(hash(vec2(seed,2))*1060.0+110.0, mod(hash(vec2(seed,3))*720.0-time*speed,720.0));
		center.x += sin(time*0.35+seed)*16.0;
		float radius = mix(0.8,2.7,hash(vec2(seed,4)));
		float point = exp(-dot(p-center,p-center)/(radius*radius));
		float pulse = 0.35+0.25*sin(time+seed);
		sum += point*mix(vec3(0.9,0.12,0.08),vec3(0.95,0.78,0.45),hash(vec2(seed,5)))*pulse;
	}
	COLOR = vec4(sum, clamp(max(sum.r,max(sum.g,sum.b)),0.0,0.65));
}
