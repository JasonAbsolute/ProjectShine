shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always;

// v3: fresh filename again (same stale-shader-cache workaround as v2).
//
// v2's phase used abs(sin(...)) directly, which spends most of its time
// near the peak (t=1, green/small) and only flickers through t=0
// (normal/big) for an instant near each zero-crossing -- screenshots kept
// missing the "big" phase entirely. Replaced with an explicit hold/
// transition state machine so both phases are visible for a real,
// independently tunable duration.

uniform sampler2D mario_tex : source_color, repeat_enable, filter_nearest;
uniform sampler2D glow_tex : source_color, repeat_enable, filter_linear_mipmap;

uniform vec2 mario_uv_scale_a = vec2(14.0, 14.0);
uniform vec2 mario_uv_scale_b = vec2(55.0, 55.0);
uniform vec2 mario_scroll_speed = vec2(0.5, 0.7);

uniform vec2 glow_uv_scale = vec2(16.0, 16.0);
uniform vec2 glow_scroll_speed = vec2(-0.2, 0.3);

uniform vec3 color_a : source_color = vec3(1.0, 1.0, 1.0);
uniform vec3 color_b : source_color = vec3(0.3, 1.0, 0.45);

uniform float hold_a_seconds = 1.6;
uniform float hold_b_seconds = 1.6;
uniform float transition_seconds = 0.35;

float phase_t(float time) {
	float cycle_total = hold_a_seconds + transition_seconds + hold_b_seconds + transition_seconds;
	float phase = mod(time, cycle_total);
	if (phase < hold_a_seconds) {
		return 0.0;
	}
	phase -= hold_a_seconds;
	if (phase < transition_seconds) {
		return smoothstep(0.0, 1.0, phase / transition_seconds);
	}
	phase -= transition_seconds;
	if (phase < hold_b_seconds) {
		return 1.0;
	}
	phase -= hold_b_seconds;
	return 1.0 - smoothstep(0.0, 1.0, phase / transition_seconds);
}

void fragment() {
	float t = phase_t(TIME);

	vec2 mario_uv_scale = mix(mario_uv_scale_a, mario_uv_scale_b, t);
	vec2 mario_uv = UV * mario_uv_scale + TIME * mario_scroll_speed;
	vec2 glow_uv = UV * glow_uv_scale + TIME * glow_scroll_speed;

	vec3 mario_sample = texture(mario_tex, mario_uv).rgb;
	vec3 glow_sample = texture(glow_tex, glow_uv).rgb;

	vec3 tint = mix(color_a, color_b, t);

	ALBEDO = clamp(mario_sample * tint * glow_sample, 0.0, 1.0);
}
