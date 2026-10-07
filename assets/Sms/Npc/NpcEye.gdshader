// SMS NPC eye (_eye_mat on the Piantas and the Toad, bmd_mat_dump.py): a translucent lit decal drawn over the face.
//   Pianta: stage 0  PREV = 1/2 + 1/2 x lit                (ONE, RASC, KONST 1/2)         -> eye_base 0.5, lit_mix 0.5, toon_mix 0
//   Toad:   stage 0  PREV = C0 + 3/8 toon + 5/8 lit         (TEXC toon, RASC, KONST 5/8, C0 = 128)  -> eye_base 0.5, lit_mix 0.625, toon_mix 0.375
//   both:   stage 1  PREV = tex x PREV, alpha = tex.a (x A0)
shader_type spatial;
render_mode cull_back, depth_draw_opaque, unshaded, fog_disabled, blend_mix;

uniform sampler2D eye_tex : source_color, filter_linear, repeat_disable;
uniform sampler2D toon_tex : filter_linear, repeat_disable;
uniform float eye_base = 0.5;
uniform float lit_mix = 0.5;
uniform float toon_mix = 0.0;
uniform vec3 sun_dir = vec3(0.348, 0.870, 0.348);
uniform vec3 sun_color = vec3(1.0);
uniform vec3 ambient = vec3(0.502);

varying vec3 v_lit0;

vec3 to_srgb(vec3 c) {
	c = clamp(c, 0.0, 1.0);
	return mix(12.92 * c, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	vec3 l = normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz);
	v_lit0 = clamp(ambient + sun_color * max(dot(n, l), 0.0), 0.0, 1.0);
}

void fragment() {
	vec4 tex = texture(eye_tex, UV);
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;
	vec3 light = clamp(eye_base + lit_mix * v_lit0 + toon_mix * toon, 0.0, 1.0);
	ALBEDO = to_linear(clamp(to_srgb(tex.rgb) * light, 0.0, 1.0));
	ALPHA = tex.a;
}
