// SMS pass 143 -- the Wind Spirit's body and lips (kazekun.bmd _mat_kazekun_body / _lips): one TEV stage, colour (ZERO, TEXC, RASC, ZERO) with the
// SUBHALF bias = tex x the lit vertex colour - 1/2, alpha (ZERO, TEXA, RASA, TEXA) = tex.a x lit.a + tex.a; blend SRCALPHA / ONE (additive), Z test
// on, Z write OFF, cull BACK, the body texture repeating. The GX lighting (one directional light + the ambient 128 / 50) is the enemy toon's.
shader_type spatial;
render_mode blend_add, cull_back, unshaded, depth_draw_never, fog_disabled;

uniform sampler2D base_tex : filter_linear, repeat_enable;     // H_kazekun_body (RGB5A3 64 x 64, wrap MIRROR on S and T: folded by hand below)
uniform vec3 sun_dir = vec3(0.348, 0.870, 0.348);              // toward the sun, world space
uniform vec3 sun_color = vec3(1.0);
uniform vec3 ambient = vec3(0.502);                            // ambColor0 (128, 128, 128)
uniform float ambient_a = 0.196;                              // ambColor0.a (50)
uniform float glow = 1.0;                                     // x the additive result (1 = the game)

varying vec3 v_lit;
varying float v_lita;

void vertex() {
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	vec3 l = normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz);
	float ndl = max(dot(n, l), 0.0);
	v_lit = clamp(ambient + sun_color * ndl, 0.0, 1.0);
	v_lita = clamp(ambient_a + ndl, 0.0, 1.0);
}

void fragment() {
	vec2 uv = 1.0 - abs(mod(UV, 2.0) - 1.0);                   // GX_MIRROR: 0..1 as is, 1..2 reflected
	vec4 t = texture(base_tex, uv);
	ALBEDO = clamp(t.rgb * v_lit - 0.5, 0.0, 1.0) * glow;
	ALPHA = clamp(t.a * v_lita + t.a, 0.0, 1.0);
}
