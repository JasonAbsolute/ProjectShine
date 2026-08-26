shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always;

// v6: fresh filename again (stale-shader-source-cache workaround).
//
// Fix: the old "multiply everything together" approach let the color tint
// (color_a/color_b) darken/discolor the sprite tile's own blue background
// pixels along with the actual character pixels -- under the green tint
// those background pixels shifted toward a muddy teal instead of staying
// the reference's clean blue. Fix: classify each mario_tex sample as
// "background" (blue-dominant) or "character" (red/yellow-dominant) via a
// cheap heuristic (nearest-filtered + flat-colored source sprite, so a hard
// threshold is fine, no blending edge cases), then composite: background
// pixels always render as bg_color (untouched by tint), character pixels
// get tint * their own sprite color. Both pass through glow_sample for the
// dot-grid brightness/shape.
//
// Also: hop_size is now an independent uniform (hop_distance_dots, in
// units of "glow dot cells") instead of being silently derived only from
// glow_uv_scale -- lets hop distance be tuned without touching dot density.

uniform sampler2D mario_tex : source_color, repeat_enable, filter_nearest;
uniform sampler2D glow_tex : source_color, repeat_enable, filter_linear_mipmap;

uniform vec2 mario_uv_scale_a = vec2(6.0, 6.0);
uniform vec2 mario_uv_scale_b = vec2(22.0, 22.0);

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

float phase_t(float time) {
	float cycle_total = hold_a_seconds + hold_b_seconds;
	float phase = mod(time, cycle_total);
	return phase < hold_a_seconds ? 0.0 : 1.0;
}

void fragment() {
	float t = phase_t(TIME);

	vec2 mario_uv_scale = mix(mario_uv_scale_a, mario_uv_scale_b, t);

	float hop_size = (hop_distance_dots) / (glow_uv_scale.x * 4.0);
	float hop_count = floor(TIME / hop_interval_seconds);
	vec2 hop_offset = hop_count * hop_size * normalize(hop_direction);

	vec2 mario_uv = UV * mario_uv_scale + hop_offset;
	vec2 glow_uv = UV * glow_uv_scale;

	vec3 mario_sample = texture(mario_tex, mario_uv).rgb;
	vec3 glow_sample = texture(glow_tex, glow_uv).rgb;

	// Blue-dominant (background) vs red/yellow-dominant (character).
	float is_background = step(mario_sample.r + mario_sample.g, mario_sample.b);

	vec3 tint = mix(color_a, color_b, t);
	vec3 character_color = tint * mario_sample;

	vec3 result = mix(character_color, bg_color, is_background) * glow_sample;
	ALBEDO = clamp(result, 0.0, 1.0);
}
