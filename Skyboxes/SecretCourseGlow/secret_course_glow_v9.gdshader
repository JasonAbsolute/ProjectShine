shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always;

// v9: fresh filename again (stale-shader-source-cache workaround).
//
// Fix: the visible hard cutoff mid-cluster was UV distortion baked into
// the sphere's own mesh UV parametrization -- standard sphere UV mapping
// is highly non-uniform (compressed near poles/seam), and at a very low
// cell count (mario_uv_scale_a=1.265, meaning the pattern only repeats
// ~1.26x across the whole sphere) a single cell spans enough of that
// distortion to visibly warp/cut. Fix: derive the tiling coordinate from
// the surface NORMAL directly (a clean longitude/latitude remap) instead
// of the mesh's raw UV -- uniform by construction, independent of
// whatever UV layout the imported GLB happened to have. Still has an
// inherent seam at the +/-180 longitude wrap and pole singularities (any
// spherical-to-planar mapping does), but now it's a single predictable
// seam instead of whatever distortion the mesh's own UV had.
//
// IMPORTANT: NORMAL in fragment() is VIEW-SPACE in Godot by default, which
// would make the whole pattern swim/rotate as the camera moves -- object
// normal must be captured in vertex() (object-space there) and passed
// through a varying instead.

varying vec3 object_normal;

uniform sampler2D mario_tex : source_color, repeat_enable, filter_nearest;
uniform sampler2D glow_tex : source_color, repeat_enable, filter_linear_mipmap;

uniform vec2 mario_uv_scale_a = vec2(1.265, 1.265);
uniform vec2 mario_uv_scale_b = vec2(4.0, 4.0);
uniform float mario_sprite_size = 0.85;

uniform vec2 hop_direction = vec2(0.6, 1.0);
uniform float hop_interval_seconds = 0.5;
uniform float hop_distance_dots = 4.025;

uniform vec2 glow_uv_scale = vec2(16.0, 16.0);

uniform vec3 bg_color : source_color = vec3(0.15, 0.35, 0.95);

uniform vec3 color_a : source_color = vec3(1.0, 1.0, 1.0);
uniform vec3 color_b : source_color = vec3(0.3, 1.0, 0.45);

uniform float hold_a_seconds = 4.3;
uniform float hold_b_seconds = 4.3;

void vertex() {
	object_normal = NORMAL; // object-space here, unlike in fragment()
}

void phase_and_local_time(float time, out float t, out float time_in_phase) {
	float cycle_total = hold_a_seconds + hold_b_seconds;
	float phase_time = mod(time, cycle_total);
	if (phase_time < hold_a_seconds) {
		t = 0.0;
		time_in_phase = phase_time;
	} else {
		t = 1.0;
		time_in_phase = phase_time - hold_a_seconds;
	}
}

void fragment() {
	// Seamless-by-construction tiling coordinate from the (object-space)
	// surface normal, instead of the mesh's own (distorted) UV.
	vec3 n = normalize(object_normal);
	float lon = atan(n.x, n.z) / TAU + 0.5;
	float lat = asin(clamp(n.y, -1.0, 1.0)) / PI + 0.5;
	vec2 sphere_uv = vec2(lon, lat);

	float t;
	float time_in_phase;
	phase_and_local_time(TIME, t, time_in_phase);

	vec2 mario_uv_scale = mix(mario_uv_scale_a, mario_uv_scale_b, t);

	float hop_size = hop_distance_dots / (glow_uv_scale.x * 4.0);
	float hop_count = floor(time_in_phase / hop_interval_seconds);
	vec2 hop_offset = hop_count * hop_size * normalize(hop_direction);

	vec2 cell_coord = sphere_uv * mario_uv_scale + hop_offset;
	vec2 cell_local = fract(cell_coord) - 0.5;
	vec2 sprite_uv = cell_local / max(mario_sprite_size, 0.0001) + 0.5;

	bool in_sprite = sprite_uv.x >= 0.0 && sprite_uv.x <= 1.0 && sprite_uv.y >= 0.0 && sprite_uv.y <= 1.0;
	vec3 mario_sample = in_sprite ? texture(mario_tex, sprite_uv).rgb : vec3(0.0, 0.0, 1.0);

	vec2 glow_uv = sphere_uv * glow_uv_scale;
	vec3 glow_sample = texture(glow_tex, glow_uv).rgb;

	float is_background = step(mario_sample.r + mario_sample.g, mario_sample.b);

	vec3 tint = mix(color_a, color_b, t);
	vec3 character_color = tint * mario_sample;

	vec3 result = mix(character_color, bg_color, is_background) * glow_sample;
	ALBEDO = clamp(result, 0.0, 1.0);
}
