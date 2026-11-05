// SMS pass 158: the BOMB BOO's ghost body (Galaxy's black Boo, BodyMat_v: the BombTeresa texture x the baked vertex colours, ambient
// 64 / 64 / 64 / 150 -- partly see-through). The texture x COLOR x `tint`, `opacity` for the screen mix, a dark-violet rim (`glow_color` x
// `glow`) so the black body reads against a dark arena, `flash` (0..1) whitens it (the carried Boo pulses with its spin; the throw's flash).
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_lambert, specular_disabled;

uniform sampler2D base_tex : source_color, filter_linear_mipmap;
uniform vec3 tint : source_color = vec3(1.0);
uniform float opacity : hint_range(0.0, 1.0) = 0.72;
uniform vec3 glow_color : source_color = vec3(0.55, 0.15, 0.85);
uniform float glow : hint_range(0.0, 3.0) = 0.9;
uniform float flash : hint_range(0.0, 1.0) = 0.0;
uniform float lift : hint_range(0.0, 2.0) = 1.0;      // the dark body lifted a little so its shape shows (1 = as the texture x colours are)

void fragment() {
	vec4 t = texture(base_tex, UV);
	vec3 c = t.rgb * COLOR.rgb * tint * lift;
	float rim = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 2.2);
	ALBEDO = mix(c, vec3(1.0), flash);
	EMISSION = glow_color * glow * rim * (1.0 - flash) + vec3(0.6) * flash;
	ROUGHNESS = 0.85;
	ALPHA = opacity * t.a;
}
