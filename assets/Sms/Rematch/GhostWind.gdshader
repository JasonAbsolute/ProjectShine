// SMS pass 169: a GHOST TORNADO's funnel (KINGBOO_REMATCH_gdoc.txt, attack 6: "Tornados come at Mario and fling him around the arena"). A cone of wind on
// a CylinderMesh (its UV: u round, v up): two layers of a soft streak tile (`wind_tex`, the port's smoke streak) scrolling round at `spin` turns a second
// and up at `rise`, the second layer the other way and finer, so the surface churns; banded by a sine along the height; fading in at the base and out at
// the top; the edges (the faces turned from the camera) more solid, the middle more see-through (a hollow funnel); a pale ghost violet (`tint`); `alpha`
// the whole; `fade` 0..1 takes it away (the dissipation). Two-sided, unshaded, mix-blended.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D wind_tex : filter_linear, repeat_enable;
uniform vec3 tint : source_color = vec3(0.86, 0.74, 1.0);
uniform float alpha : hint_range(0.0, 1.0) = 0.8;
uniform float spin = 1.2;
uniform float rise = 0.5;
uniform float bands = 9.0;
uniform float fade : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec2 uv1 = vec2(UV.x * 3.0 + TIME * spin, UV.y * 2.0 - TIME * rise);
	vec2 uv2 = vec2(UV.x * 5.0 - TIME * spin * 0.7, UV.y * 3.0 - TIME * rise * 1.6);
	float s1 = texture(wind_tex, uv1).a;
	float s2 = texture(wind_tex, uv2).a;
	float churn = pow(clamp(s1 * 0.9 + s2 * 0.7, 0.0, 1.0), 1.4);
	float band = 0.65 + 0.35 * sin((UV.y * bands - TIME * 2.5) * 6.2831);
	float base = smoothstep(0.0, 0.12, UV.y);
	float top = 1.0 - smoothstep(0.7, 1.0, UV.y);
	float facing = abs(dot(normalize(NORMAL), normalize(VIEW)));
	float edge = mix(1.0, 0.45, facing);                       // hollow: the middle thinner than the rim
	float a = churn * band * base * top * edge * alpha * (1.0 - fade);
	ALBEDO = tint * (0.7 + 0.7 * churn);
	ALPHA = clamp(a, 0.0, 1.0);
}
