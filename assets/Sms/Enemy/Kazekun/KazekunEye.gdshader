// SMS pass 143 -- the Wind Spirit's eyes (kazekun.bmd _mat_kazekun_eye): one TEV stage, colour (ZERO, TEXC, RASC, ZERO) = tex x the lit vertex colour,
// alpha tex.a x lit.a; opaque, cull BACK. The GX lighting is the enemy toon's.
shader_type spatial;
render_mode cull_back, unshaded, fog_disabled;

uniform sampler2D base_tex : filter_linear, repeat_disable;    // H_kazekun_eye1 (IA8 64 x 64)
uniform vec3 sun_dir = vec3(0.348, 0.870, 0.348);
uniform vec3 sun_color = vec3(1.0);
uniform vec3 ambient = vec3(0.502);

varying vec3 v_lit;

void vertex() {
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	vec3 l = normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz);
	v_lit = clamp(ambient + sun_color * max(dot(n, l), 0.0), 0.0, 1.0);
}

void fragment() {
	ALBEDO = clamp(texture(base_tex, UV).rgb * v_lit, 0.0, 1.0);
}
