// Pass 107: ms_m_tokeos, the goop melting off Mario in water (BSP1): a Direction quad rolled flat on the water along its drift (see
// gen_fx_goop.py), blend SRCALPHA / INVSRCALPHA, no alpha test, z test no write; colour lerp(env -> prm, I) picked at random from a 6-entry
// table (4 brown, 1 half, 1 pale).
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear, repeat_disable;
uniform int quad_kind = 0;              // 0 = flat in the particle frame (X across, Z forward: the footprint); 1 = flat along the velocity (align_y: Y = the velocity, Z vertical -> the plain XY quad: the melt); 2 = a DirBillboard (camera-facing, Y along the velocity: the fling)
uniform vec4 esp1_x = vec4(0.0, 1.0, 1.0, 1.0);   // (in timing, out timing, in value, out value) -- JPADrawCalcScaleX
uniform vec4 esp1_y = vec4(0.0, 1.0, 1.0, 1.0);
uniform vec3 prm0 = vec3(1.0);         // the colour table (display space): prm / env at the random index 0, the middle, the top
uniform vec3 prm1 = vec3(1.0);
uniform vec3 prm2 = vec3(1.0);
uniform vec3 env0 = vec3(0.0);
uniform vec3 env1 = vec3(0.0);
uniform vec3 env2 = vec3(0.0);
uniform float pick1 = 0.3333;          // COLOR.r (a uniform random 0..1 per particle) below pick1 -> index 0, below pick2 -> the middle, else the top
uniform float pick2 = 0.6667;
uniform float alpha_ref = 0.0;         // the GX alpha compare (GEQUAL ref / 255); 0 = none

varying float life_phase;
varying float pick;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

float esp1(float t, vec4 p) {
	// JPADrawVisitor.cpp 1478-1497: t < in -> v_in + (1 - v_in) t / in; t > out -> 1 + (v_out - 1)(t - out) / (1 - out); else 1
	if (p.x > 0.0 && t < p.x) return p.z + (1.0 - p.z) * t / p.x;
	if (p.y < 1.0 && t > p.y) return 1.0 + (p.w - 1.0) * (t - p.y) / (1.0 - p.y);
	return 1.0;
}

void vertex() {
	life_phase = INSTANCE_CUSTOM.y;
	pick = COLOR.r;
	float t = clamp(life_phase, 0.0, 1.0);
	vec2 v = VERTEX.xy * vec2(esp1(t, esp1_x), esp1(t, esp1_y));   // the QuadMesh faces +Z: its vertices in XY, V up the +Y side
	// Godot (scene_shader_forward_clustered.cpp 839): a shader that writes POSITION ANYWHERE gets OVERRIDE_POSITION for the whole shader and the
	// default gl_Position is skipped on every path (position = vec4(1.0): the quad vanishes) -- so every kind writes it (pass 107b)
	if (quad_kind == 0) {
		VERTEX = vec3(v.x, 0.0, v.y);
		NORMAL = vec3(0.0, 1.0, 0.0);
		POSITION = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	} else if (quad_kind == 1) {
		// align_y (particle_process_material.cpp 1203-1212): Y = the velocity, Z = the emitter's X x Y (vertical for a horizontal drift),
		// X = Y x Z (the horizontal perpendicular) -> the plain XY quad already lies flat, V along the drift
		VERTEX = vec3(v.x, v.y, 0.0);
		POSITION = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	} else {
		vec3 center_v = (VIEW_MATRIX * MODEL_MATRIX[3]).xyz;
		vec3 vel_w = MODEL_MATRIX[1].xyz;
		float sx = length(MODEL_MATRIX[0].xyz);
		float sy = length(vel_w);
		vec3 vel_v = (VIEW_MATRIX * vec4(vel_w, 0.0)).xyz;
		vec2 up = length(vel_v.xy) > 1e-6 ? normalize(vel_v.xy) : vec2(0.0, 1.0);
		vec2 right = vec2(up.y, -up.x);
		vec3 p = center_v + vec3(right * (v.x * sx) + up * (v.y * sy), 0.0);
		POSITION = PROJECTION_MATRIX * vec4(p, 1.0);
	}
}

void fragment() {
	vec4 t = texture(tex, UV);
	vec3 prm = pick < pick1 ? prm0 : (pick < pick2 ? prm1 : prm2);
	vec3 env = pick < pick1 ? env0 : (pick < pick2 ? env1 : env2);
	ALBEDO = to_linear(mix(env, prm, t.r));
	ALPHA = t.a * COLOR.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_ref;
}
