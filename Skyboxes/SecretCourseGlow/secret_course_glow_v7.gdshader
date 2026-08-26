shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always;

// v7: fresh filename again (stale-shader-source-cache workaround).
//
// mario_uv_scale_a/b previously controlled BOTH how large each sprite tile
// was AND how far apart tiles sat, coupled together (it's just a tiling
// frequency -- more tiles = smaller AND closer, fewer tiles = bigger AND
// farther apart). Split into two independent knobs:
//   - mario_uv_scale_a/b: CELL pitch -- how many grid cells fit across the
//     UV range (spacing/density, same role as before).
//   - mario_sprite_size: FILL fraction (0..1) of each cell the actual
//     sprite occupies. 1.0 = fills the whole cell, no gap (old behavior).
//     Lower values shrink the sprite within its cell, opening a gap/margin
//     around it -- independent of cell pitch, so size and spacing can be
//     tuned separately.
// Sample positions that land in that gap (outside the shrunk sprite's own
// 0..1 UV bounds) are treated as background blue directly, same as the
// sprite's own blue-background pixels.

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

float phase_t(float time) {
	float cycle_total = hold_a_seconds + hold_b_seconds;
	float phase = mod(time, cycle_total);
	return phase < hold_a_seconds ? 0.0 : 1.0;
}

void fragment() {
	float t = phase_t(TIME);

	vec2 mario_uv_scale = mix(mario_uv_scale_a, mario_uv_scale_b, t);

	float hop_size = hop_distance_dots / (glow_uv_scale.x * 4.0);
	float hop_count = floor(TIME / hop_interval_seconds);
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
