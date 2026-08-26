shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always;

// v4: fresh filename again (stale-shader-source-cache workaround, same as
// v2/v3).
//
// Frame-by-frame video reference showed two corrections vs v3:
//  1. The normal<->green swap is a HARD CUT, not a crossfade -- confirmed
//     by consecutive frames 3.97s (nothing) -> 4.00s (full-size block
//     already at full opacity, zero fade-in visible at 30fps).
//  2. mario_tex needs to actually scroll ("slowly climb") -- the apparent
//     red-then-yellow drift within a single "normal" hold is most likely
//     just the sprite's own two-tone coloring (red cap, yellow skin/body
//     baked into one 64x64 tile) scrolling through the fixed camera
//     window over time, not a separate color animation. glow_tex (the
//     background dot grid) stays static per direct user confirmation.

uniform sampler2D mario_tex : source_color, repeat_enable, filter_nearest;
uniform sampler2D glow_tex : source_color, repeat_enable, filter_linear_mipmap;

uniform vec2 mario_uv_scale_a = vec2(6.0, 6.0);
uniform vec2 mario_uv_scale_b = vec2(22.0, 22.0);
// Slow diagonal climb -- deliberately small; this is meant to read as a
// slow drift over several seconds, not a fast scroll.
uniform vec2 mario_scroll_speed = vec2(0.04, 0.07);

uniform vec2 glow_uv_scale = vec2(16.0, 16.0);

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
	vec2 mario_uv = UV * mario_uv_scale + TIME * mario_scroll_speed;
	vec2 glow_uv = UV * glow_uv_scale;

	vec3 mario_sample = texture(mario_tex, mario_uv).rgb;
	vec3 glow_sample = texture(glow_tex, glow_uv).rgb;

	vec3 tint = mix(color_a, color_b, t);

	ALBEDO = clamp(mario_sample * tint * glow_sample, 0.0, 1.0);
}
