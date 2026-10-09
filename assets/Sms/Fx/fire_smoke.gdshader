// ms_npc_kokuen (the burning NPC's black smoke, BSP1): Billboard, blend SRCALPHA + INVSRCALPHA, z test on / no write; colour = lerp(env -> prm,
// TEXC) with BOTH registers keyed over the particle's life (colour calc type 3 = Merge: the 20-frame key table stretched over the life,
// JPADrawCalcColorAnmFrameMerge) -- prm (87,8,0) -> (45,34,34) -> (72,72,72), env (255,111,48) -> (96,96,96) -> (120,120,120) at frames 2 / 8 / 15:
// a dark-red / orange puff that goes grey -- alpha = TEXA x prmA x the ESP1 fade (COLOR.a from the ramp). The two colour ramps are sampled by
// the particle's life phase (INSTANCE_CUSTOM.y, ParticleProcessMaterial's CUSTOM.y). The texture is the raw IA8 (I in .r, A in .a).
// Pass 99: the quad is a BILLBOARD (the JPA's BSP1 shape) turned by the particle's own angle (CUSTOM.x, the ESP1 rotate) -- pass 98 left it flat
// in the emitter's XY, so the puffs drew edge-on from most angles. Also the ENM_WATHIT water-hit mark (WaterHitMark.tscn): the same TEV.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear_mipmap, repeat_disable;
uniform sampler2D prm_ramp : filter_linear, repeat_disable;     // the dark pixels' colour over the life (display-space values)
uniform sampler2D env_ramp : filter_linear, repeat_disable;     // the bright pixels' colour over the life

varying float life_phase;                                       // INSTANCE_CUSTOM is vertex-only: carried to the fragment here

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	life_phase = INSTANCE_CUSTOM.y;
	// the billboard: the view's axes at the particle's position, the particle's scale kept, rolled by its angle (StandardMaterial3D's
	// BILLBOARD_PARTICLES does the same with CUSTOM.x = the ESP1 rotate)
	float a = INSTANCE_CUSTOM.x;
	mat4 world = mat4(normalize(INV_VIEW_MATRIX[0]) * length(MODEL_MATRIX[0].xyz), normalize(INV_VIEW_MATRIX[1]) * length(MODEL_MATRIX[1].xyz),
	                  normalize(INV_VIEW_MATRIX[2]) * length(MODEL_MATRIX[2].xyz), MODEL_MATRIX[3]);
	world = world * mat4(vec4(cos(a), -sin(a), 0.0, 0.0), vec4(sin(a), cos(a), 0.0, 0.0), vec4(0.0, 0.0, 1.0, 0.0), vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_MATRIX = VIEW_MATRIX * world;
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	vec4 t = texture(tex, UV);
	float life = clamp(life_phase, 0.0, 1.0);
	vec3 prm = texture(prm_ramp, vec2(life, 0.5)).rgb;
	vec3 env = texture(env_ramp, vec2(life, 0.5)).rgb;
	ALBEDO = to_linear(mix(env, prm, t.r));
	ALPHA = t.a * COLOR.a;
}
