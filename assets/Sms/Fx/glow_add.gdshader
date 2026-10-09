// ms_moe_fire_b (the burning NPC's glow, BSP1): Billboard, blend SRCALPHA + ONE (additive), z test on / no write; colour = prm FLAT
// (255,172,101) -- the texture only shapes the alpha: TEXA (= I of the I8 radial gradient) x prmA x the ESP1 fade (COLOR.a). The texture is
// the raw I8 (I in .r and .a).
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear_mipmap, repeat_disable;
uniform vec3 prm_color = vec3(1.0, 0.675, 0.396);        // (255, 172, 101) / 255

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
	float a = texture(tex, UV).a;
	ALBEDO = to_linear(prm_color);
	ALPHA = a * COLOR.a;
}
