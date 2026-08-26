shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always;

// v5: fresh filename again (stale-shader-source-cache workaround).
//
// User watching it live: motion needs to be a discrete HOP to the next
// dot-grid cell every ~15 frames (0.5s @ 30fps), not a continuous smooth
// drift -- matches the LED-dot-matrix aesthetic (the whole background is a
// grid, so the sprite pattern should snap to that same grid rather than
// slide freely). Direction (up-and-right) was already correct with
// continuous scrolling, so kept as-is, just quantized.
//
// glow_tex is a 4x4 array of dots baked into ONE tile (128x128, confirmed
// by inspection), so one dot-cell spacing in UV space = 1/(glow_uv_scale*4).

uniform sampler2D mario_tex : source_color, repeat_enable, filter_nearest;
uniform sampler2D glow_tex : source_color, repeat_enable, filter_linear_mipmap;

uniform vec2 mario_uv_scale_a = vec2(6.0, 6.0);
uniform vec2 mario_uv_scale_b = vec2(22.0, 22.0);

uniform vec2 hop_direction = vec2(0.6, 1.0); // up-right diagonal; normalized in-shader
uniform float hop_interval_seconds = 0.5; // 15 frames @ 30fps

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

	float hop_size = 1.0 / (glow_uv_scale.x * 4.0);
	float hop_count = floor(TIME / hop_interval_seconds);
	vec2 hop_offset = hop_count * hop_size * normalize(hop_direction);

	vec2 mario_uv = UV * mario_uv_scale + hop_offset;
	vec2 glow_uv = UV * glow_uv_scale;

	vec3 mario_sample = texture(mario_tex, mario_uv).rgb;
	vec3 glow_sample = texture(glow_tex, glow_uv).rgb;

	vec3 tint = mix(color_a, color_b, t);

	ALBEDO = clamp(mario_sample * tint * glow_sample, 0.0, 1.0);
}
