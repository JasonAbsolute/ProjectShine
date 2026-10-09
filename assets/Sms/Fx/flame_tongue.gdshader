// ms_moe_fire_a (the burning NPC's flame tongues, BSP1): StripeCross along the velocity (two crossed quads, align_y), blend SRCALPHA +
// INVSRCALPHA, z test on / no write; colour = TEXC x prm + env with prm (255,228,170) and env keyed (255,54,0) -> (255,118,31) over 5 frames,
// colour calc type 1 = Repeat (JPADrawCalcColorAnmFrameRepeat: frame = age % 6) -- a 6-frame red -> orange flicker per particle; alpha = TEXA x
// prmA (231/255), no ESP1 alpha. The age comes from the life phase (INSTANCE_CUSTOM.y) x life_frames. The texture is the raw IA8.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear_mipmap, repeat_disable;
uniform vec3 prm_color = vec3(1.0, 0.894, 0.667);        // (255, 228, 170) / 255
uniform float prm_alpha = 0.906;                           // 231 / 255
uniform vec3 env_a = vec3(1.0, 0.212, 0.0);               // key frame 0: (255, 54, 0)
uniform vec3 env_b = vec3(1.0, 0.463, 0.122);             // key frame 5: (255, 118, 31)
uniform float life_frames = 15.0;
uniform float cycle_frames = 6.0;                          // mColorRegAnmMaxFrm 5 + 1

varying float life_phase;                                  // INSTANCE_CUSTOM is vertex-only: carried to the fragment here

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	life_phase = INSTANCE_CUSTOM.y;
}

void fragment() {
	vec4 t = texture(tex, UV);
	float age = clamp(life_phase, 0.0, 1.0) * life_frames;
	float frame = mod(age, cycle_frames);
	vec3 env = mix(env_a, env_b, clamp(frame / (cycle_frames - 1.0), 0.0, 1.0));
	ALBEDO = to_linear(clamp(t.r * prm_color + env, 0.0, 1.0));
	ALPHA = t.a * prm_alpha;
}
