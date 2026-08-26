shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always;

// Recreation of SMS's secret-course background (coro_ex2/map/sky):
// two tiled/scrolling textures multiplied together, times an animated
// color tint. Decoded from the original TEV fragment shader:
//   color = clamp(marioSprite.rgb * colorRegister * glowDots.rgb, 0, 1)
// Both source textures sample the SAME uv, each through its own
// independent scale+scroll (the original used a per-texture 2D
// transform matrix; scale+speed here is the equivalent).
//
// mario_tex uses nearest filtering — it's an 8-bit sprite tile, keep
// the blocky pixel edges instead of letting mipmaps blur it to mush.

uniform sampler2D mario_tex : source_color, repeat_enable, filter_nearest;
uniform sampler2D glow_tex : source_color, repeat_enable, filter_linear_mipmap;

uniform vec2 mario_uv_scale = vec2(40.0, 40.0);
uniform vec2 mario_scroll_speed = vec2(0.6, 0.9);

uniform vec2 glow_uv_scale = vec2(60.0, 60.0);
uniform vec2 glow_scroll_speed = vec2(-0.3, 0.45);

uniform vec3 color_a : source_color = vec3(1.0, 1.0, 1.0);
uniform vec3 color_b : source_color = vec3(0.3, 1.0, 0.45);
uniform float color_cycle_seconds = 3.0;

void fragment() {
	vec2 mario_uv = UV * mario_uv_scale + TIME * mario_scroll_speed;
	vec2 glow_uv = UV * glow_uv_scale + TIME * glow_scroll_speed;

	vec3 mario_sample = texture(mario_tex, mario_uv).rgb;
	vec3 glow_sample = texture(glow_tex, glow_uv).rgb;

	float t = smoothstep(0.0, 1.0, abs(sin(TIME * PI / color_cycle_seconds)));
	vec3 tint = mix(color_a, color_b, t);

	ALBEDO = clamp(mario_sample * tint * glow_sample, 0.0, 1.0);
}
