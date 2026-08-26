shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always;

// v8: fresh filename again (stale-shader-source-cache workaround).
//
// Fix: hop_count previously accumulated from TIME=0 forever, never
// resetting. Since mario_uv_scale (the cell grid density) changes
// abruptly at every phase cut (6x6 <-> 22x22), carrying that same
// accumulated offset into a differently-sized grid put cells at an
// effectively arbitrary alignment relative to the sprite fill window --
// occasionally landing a sprite straddling a cell boundary and getting it
// clipped right at/after a transition. Fix: hop_count now resets to 0 at
// the START of each hold period (A or B) instead of running continuously
// across the whole cycle, so every phase begins from the same clean
// alignment every time.

uniform sampler2D mario_tex : source_color, repeat_enable, filter_nearest;
uniform sampler2D glow_tex : source_color, repeat_enable, filter_linear_mipmap;

uniform vec2 mario_uv_scale_a = vec2(6.0, 6.0); // cell spacing/density, normal phase
uniform vec2 mario_uv_scale_b = vec2(22.0, 22.0); // cell spacing/density, green phase
uniform float mario_sprite_size = 0.85; // 0..1, fraction of each cell the sprite fills

uniform vec2 hop_direction = vec2(0.6, 1.0); // up-right diagonal; normalized in-shader
uniform float hop_interval_seconds = 0.5; // time between hops (15 frames @ 30fps)
uniform float hop_distance_dots = 1.0; // how many glow-dot-cells each hop covers

uniform vec2 glow_uv_scale = vec2(16.0, 16.0);

// Background dot color -- ALWAYS this color, regardless of color_a/color_b.
uniform vec3 bg_color : source_color = vec3(0.15, 0.35, 0.95);

uniform vec3 color_a : source_color = vec3(1.0, 1.0, 1.0);
uniform vec3 color_b : source_color = vec3(0.3, 1.0, 0.45);

uniform float hold_a_seconds = 4.3;
uniform float hold_b_seconds = 4.3;

// Returns {phase 0/1, time elapsed since THIS specific hold period started}.
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
	float t;
	float time_in_phase;
	phase_and_local_time(TIME, t, time_in_phase);

	vec2 mario_uv_scale = mix(mario_uv_scale_a, mario_uv_scale_b, t);

	float hop_size = hop_distance_dots / (glow_uv_scale.x * 4.0);
	float hop_count = floor(time_in_phase / hop_interval_seconds); // resets to 0 every phase
	vec2 hop_offset = hop_count * hop_size * normalize(hop_direction);

	vec2 cell_coord = UV * mario_uv_scale + hop_offset;
	vec2 cell_local = fract(cell_coord) - 0.5; // -0.5..0.5, centered in its cell
	vec2 sprite_uv = cell_local / max(mario_sprite_size, 0.0001) + 0.5;

	bool in_sprite = sprite_uv.x >= 0.0 && sprite_uv.x <= 1.0 && sprite_uv.y >= 0.0 && sprite_uv.y <= 1.0;
	vec3 mario_sample = in_sprite ? texture(mario_tex, sprite_uv).rgb : vec3(0.0, 0.0, 1.0);

	vec2 glow_uv = UV * glow_uv_scale;
	vec3 glow_sample = texture(glow_tex, glow_uv).rgb;

	// Blue-dominant (background, incl. the padding gap above) vs red/yellow-dominant (character).
	float is_background = step(mario_sample.r + mario_sample.g, mario_sample.b);

	vec3 tint = mix(color_a, color_b, t);
	vec3 character_color = tint * mario_sample;

	vec3 result = mix(character_color, bg_color, is_background) * glow_sample;
	ALBEDO = clamp(result, 0.0, 1.0);
}
