// SMS pass 170: a GUST RIBBON -- one of the streaks of wind spiralling up a ghost tornado (Jaysonn: "the wind needs to look more like a gust of wind").
// A helix ribbon mesh (SmsTornado builds it: u along the ribbon, v across it): the port's smoke streak scrolling along it at `speed`, soft across (a sine
// over v), fading in at the foot and out at the head, brightest where the streak is; a pale ghost white-violet (`tint`), `alpha` the whole, `fade` 0..1
// takes it away. Two-sided, unshaded, mix-blended.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D streak_tex : filter_linear, repeat_enable;
uniform vec3 tint : source_color = vec3(0.94, 0.9, 1.0);
uniform float alpha : hint_range(0.0, 1.0) = 0.85;
uniform float speed = 1.6;
uniform float tiles = 2.5;
uniform float fade : hint_range(0.0, 1.0) = 0.0;
uniform float phase = 0.0;

void fragment() {
	float s = texture(streak_tex, vec2(UV.x * tiles - TIME * speed + phase, UV.y)).a;
	float across = sin(UV.y * 3.14159);
	float along = smoothstep(0.0, 0.15, UV.x) * (1.0 - smoothstep(0.75, 1.0, UV.x));
	float a = (0.35 + 0.65 * s) * across * across * along * alpha * (1.0 - fade);
	ALBEDO = tint * (0.8 + 0.4 * s);
	ALPHA = clamp(a, 0.0, 1.0);
}
