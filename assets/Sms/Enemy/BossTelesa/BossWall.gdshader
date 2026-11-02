// BossWall.gdshader -- the casino boss room's WALL (boss_wall.bmd, its one material _B2f_wall; TSirenabossWall in MapObjSirena.cpp) for SMS pass 153k.
// The game's TEV, four stages, no lighting (the colour channel reads the material REGISTER (96, 73, 96), not the vertex colours the bmd also carries):
//   stage 0: TEX0 (P_casino_floor -- despite the name the GOLD STARS ON PURPLE, CMPR 128 x 128, tiled 24 round x 8 up the wall) x the register colour
//   stage 1..3: + K0 / K1 / K2 x TEXn (P_telusaroom1_i, an I8 of two soft SPOTS, through TEXMTX1..3: scale 0.125 about the tile's centre, translated)
//   K0 (111, 0, 24) red, K1 (96, 96, 0) yellow, K2 (24, 0, 72) violet; clamped; opaque (no blend, alpha test ALWAYS), z write, cull BACK (seen from inside).
// TSirenabossWall plays its three btks AT ONCE (TMultiBtk(3)): boss_wall_01 drives TEXMTX1, _02 TEXMTX2, _03 TEXMTX3 (texture slots 1..3), 30 frames a second:
//   01 (600 f): S eases 0 -> -0.7 -> 0, T runs 0 -> 2 (the spots RISE, swaying);  02 (850 f): S runs 0.5 -> -2.5, T eases 0 -> 0.8 -> 0;
//   03 (930 f): S runs 0 -> 1, T eases 0.5 -> 1.5 -> 0.5. The eases are Hermite keys with zero tangents: (1 - cos) / 2 here.
// At scale 0.125 one spot tile spans a third of the way round and the whole height: the casino's coloured lights, ~20 m soft discs drifting over the wall.
// Everything is computed in the TEV's display space and converted to linear once at the end (as the port's other SMS shaders do). Fog as the table's.
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque, shadows_disabled;

uniform sampler2D stars_tex : filter_linear_mipmap, repeat_enable;   // boss_wall_stars.png (raw: the TEV multiplies display values)
uniform sampler2D spots_tex : filter_linear_mipmap, repeat_enable;   // boss_wall_spots.png (the I8 in .r)
uniform vec3 mat_color = vec3(0.3765, 0.2863, 0.3765);              // GxMaterialColor0 (96, 73, 96) / 255
uniform vec3 k0 = vec3(0.4353, 0.0, 0.0941);                         // (111, 0, 24)
uniform vec3 k1 = vec3(0.3765, 0.3765, 0.0);                         // (96, 96, 0)
uniform vec3 k2 = vec3(0.0941, 0.0, 0.2824);                         // (24, 0, 72)
uniform float spot_scale = 0.125;                                    // TEXMTX1..3's scale (about the tile's centre)
uniform float spot_speed = 1.0;                                      // x the btks' 30 frames a second (0 = the spots stand still)
uniform float spot_strength = 1.0;                                   // DEVIATION knob: x the three spot layers (0 = none)
uniform float brightness = 1.0;                                      // DEVIATION knob: x the whole wall (the game's room is dark)

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

float hump(float x) {            // 0 at the loop's ends, 1 at its middle, flat at both: the btk's zero-tangent keys
	return 0.5 - 0.5 * cos(6.2831853 * x);
}

vec2 layer_uv(vec2 uv, vec2 trans) {
	return spot_scale * (uv - 0.5) + 0.5 + trans;
}

void fragment() {
	float f = TIME * 30.0 * spot_speed;                                              // the game's frames
	float p1 = fract(f / 600.0), p2 = fract(f / 850.0), p3 = fract(f / 930.0);
	vec2 t1 = vec2(-0.7 * hump(p1), 2.0 * p1);
	vec2 t2 = vec2(0.5 - 3.0 * p2, 0.8 * hump(p2));
	vec2 t3 = vec2(p3, 0.5 + hump(p3));
	vec3 base = clamp(texture(stars_tex, UV).rgb * mat_color, 0.0, 1.0);             // stage 0
	float s1 = texture(spots_tex, layer_uv(UV, t1)).r;
	float s2 = texture(spots_tex, layer_uv(UV, t2)).r;
	float s3 = texture(spots_tex, layer_uv(UV, t3)).r;
	vec3 col = clamp(base + spot_strength * (k0 * s1 + k1 * s2 + k2 * s3), 0.0, 1.0);  // stages 1..3
	ALBEDO = to_linear(clamp(col * brightness, 0.0, 1.0));
}
