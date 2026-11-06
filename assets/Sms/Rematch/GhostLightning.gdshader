// SMS pass 166: a WALL OF GHOST LIGHTNING (KINGBOO_REMATCH_gdoc.txt, attack 3: "two walls of lightning close in on Mario from either side"). A quad
// WallLength wide and WallHeight tall: `tiles` columns of the shock's bolt sheet (P_ms_biri_l1, the Electrokoopa's streak, white with its alpha), each
// column jittered sideways, flipped and re-rolled `jitter_hz` times a second by a hash of (column, time) so the bolts jump about; the shock's colour ramp
// (ms_m_biri_a: cyan (111, 255, 255) toward blue (63, 87, 255)) by the bolt's brightness, additive, two-sided; the whole flickering by `flicker`; a hot
// core at the floor (`floor_glow`), the top fading out. `warn` 0..1 dims it (the tell: the walls shown before they close); `flash` whitens it (the meet).
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D bolt_tex : filter_linear, repeat_enable;
uniform vec3 color_a : source_color = vec3(0.435, 1.0, 1.0);
uniform vec3 color_b : source_color = vec3(0.25, 0.34, 1.0);
uniform float tiles = 10.0;
uniform float jitter_hz = 18.0;
uniform float flicker : hint_range(0.0, 1.0) = 0.35;
uniform float intensity : hint_range(0.0, 3.0) = 1.2;
uniform float floor_glow : hint_range(0.0, 2.0) = 0.6;
uniform float warn : hint_range(0.0, 1.0) = 0.0;
uniform float flash : hint_range(0.0, 1.0) = 0.0;
uniform float seed = 0.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7)) + seed * 17.0) * 43758.5453);
}

void fragment() {
	float t = floor(TIME * jitter_hz);
	float col = floor(UV.x * tiles);
	float fx = fract(UV.x * tiles);
	float h1 = hash(vec2(col, t)), h2 = hash(vec2(col + 100.0, t)), h3 = hash(vec2(col + 200.0, t));
	float x = fx + (h1 - 0.5) * 0.5;                            // the column's bolt shoved sideways
	if (h2 > 0.5) x = 1.0 - x;                                 // or flipped
	float y = UV.y * (0.8 + 0.4 * h3) + h1 * 0.3;              // stretched and slid along its length
	float a = texture(bolt_tex, vec2(x, y)).a;
	float a2 = texture(bolt_tex, vec2(fract(UV.x * tiles * 0.5 + h3), UV.y * 1.3 + t * 0.07)).a * 0.5;   // a second, wider layer behind
	float bolt = max(a, a2);
	float top = 1.0 - smoothstep(0.55, 1.0, UV.y);              // thinning toward the top
	float base = exp(-UV.y * 6.0) * floor_glow;                 // the hot core at the floor
	float flick = 1.0 - flicker * hash(vec2(t, 7.0));
	vec3 ramp = mix(color_b, color_a, clamp(bolt * 1.5, 0.0, 1.0));
	vec3 c = (ramp * bolt * top + ramp * base) * intensity * flick * mix(1.0, 0.35, warn);
	ALBEDO = mix(c, vec3(1.0) * (bolt * top + base) * 2.0, flash);
	ALPHA = clamp(bolt * top * 0.9 + base, 0.0, 1.0);
}
