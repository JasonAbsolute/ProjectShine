shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always;

// v2: fresh filename to dodge the editor's stale-shader-source cache
// (write_text overwrites on disk fine, but the already-loaded Shader
// resource doesn't reload from it -- a new path guarantees a fresh load).
//
// Reference footage shows the sprite cluster's SIZE tied to which color
// phase it's in: big when normal-tinted (color_a), small when green-tinted
// (color_b) -- not just a plain color swap. mario_uv_scale now interpolates
// between mario_uv_scale_a (big sprites, small scale) and
// mario_uv_scale_b (small sprites, large scale) on the same phase driving
// the color mix. Dot grid density also reduced -- reference dots are large
// and clearly spaced, not a fine mesh.

uniform sampler2D mario_tex : source_color, repeat_enable, filter_nearest;
uniform sampler2D glow_tex : source_color, repeat_enable, filter_linear_mipmap;

uniform vec2 mario_uv_scale_a = vec2(14.0, 14.0);
uniform vec2 mario_uv_scale_b = vec2(55.0, 55.0);
uniform vec2 mario_scroll_speed = vec2(0.5, 0.7);

uniform vec2 glow_uv_scale = vec2(22.0, 22.0);
uniform vec2 glow_scroll_speed = vec2(-0.25, 0.35);

uniform vec3 color_a : source_color = vec3(1.0, 1.0, 1.0);
uniform vec3 color_b : source_color = vec3(0.3, 1.0, 0.45);
uniform float color_cycle_seconds = 3.0;

void fragment() {
	float t = smoothstep(0.0, 1.0, abs(sin(TIME * PI / color_cycle_seconds)));

	vec2 mario_uv_scale = mix(mario_uv_scale_a, mario_uv_scale_b, t);
	vec2 mario_uv = UV * mario_uv_scale + TIME * mario_scroll_speed;
	vec2 glow_uv = UV * glow_uv_scale + TIME * glow_scroll_speed;

	vec3 mario_sample = texture(mario_tex, mario_uv).rgb;
	vec3 glow_sample = texture(glow_tex, glow_uv).rgb;

	vec3 tint = mix(color_a, color_b, t);

	ALBEDO = clamp(mario_sample * tint * glow_sample, 0.0, 1.0);
}
