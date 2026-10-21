// Pass 106: the trample dust's quads (ms_fumi_a / c). Colour as JPA's: lerp(env -> prm, I) of an IA texture, or prm FLAT (colour in-select
// 5), both registers keyed over the particle's life (Merge: the key table stretched over the life -- the ramps are sampled by the life phase,
// INSTANCE_CUSTOM.y); alpha = A x prm.a (the prm ramp's alpha) x COLOR.a (the ESP1 fade). Three quad kinds: a Billboard rolled by the
// particle's angle (CUSTOM.x) + angle_offset; a DirBillboard (dir_billboard: a camera-facing quad whose Y runs along the particle's
// on-screen velocity, from the align_y transform's Y column -- JpaDirBillboard's construction); an aligned cross (aligned_cross: the mesh
// drawn as the align_y transform has it -- two crossed quads along the velocity in world space, a DirectionCross). INSTANCE_CUSTOM is
// vertex-only (pass 98): carried to the fragment by varyings. GX colours are display-referred: the mix is raw, the result put to linear.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear_mipmap, repeat_disable;
uniform sampler2D prm_ramp : filter_linear, repeat_disable;
uniform sampler2D env_ramp : filter_linear, repeat_disable;
uniform bool flat_prm = false;
uniform bool dir_billboard = false;
uniform bool aligned_cross = false;
uniform float angle_offset = 0.0;

varying float life_phase;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	life_phase = INSTANCE_CUSTOM.y;
	if (aligned_cross) {
		// the align_y transform as the process material built it: Y = the velocity, the mesh drawn in it (a DirectionCross). POSITION written
		// here too: a shader that writes POSITION anywhere skips the default transform on EVERY path (OVERRIDE_POSITION) -- pass 107b's fix;
		// the marks and spokes had been invisible since 106
		POSITION = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	} else if (dir_billboard) {
		vec3 center_v = (VIEW_MATRIX * MODEL_MATRIX[3]).xyz;
		vec3 vel_w = MODEL_MATRIX[1].xyz;
		float sx = length(MODEL_MATRIX[0].xyz);
		float sy = length(vel_w);
		vec3 vel_v = (VIEW_MATRIX * vec4(vel_w, 0.0)).xyz;
		vec2 up = length(vel_v.xy) > 1e-6 ? normalize(vel_v.xy) : vec2(0.0, 1.0);
		float a = angle_offset;
		up = vec2(up.x * cos(a) - up.y * sin(a), up.x * sin(a) + up.y * cos(a));
		vec2 right = vec2(up.y, -up.x);
		vec3 p = center_v + vec3(right * (VERTEX.x * sx) + up * (VERTEX.y * sy), 0.0);
		POSITION = PROJECTION_MATRIX * vec4(p, 1.0);
	} else {
		float a = INSTANCE_CUSTOM.x + angle_offset;
		mat4 world = mat4(normalize(INV_VIEW_MATRIX[0]) * length(MODEL_MATRIX[0].xyz), normalize(INV_VIEW_MATRIX[1]) * length(MODEL_MATRIX[1].xyz),
		                  normalize(INV_VIEW_MATRIX[2]) * length(MODEL_MATRIX[2].xyz), MODEL_MATRIX[3]);
		world = world * mat4(vec4(cos(a), -sin(a), 0.0, 0.0), vec4(sin(a), cos(a), 0.0, 0.0), vec4(0.0, 0.0, 1.0, 0.0), vec4(0.0, 0.0, 0.0, 1.0));
		MODELVIEW_MATRIX = VIEW_MATRIX * world;
		MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
		POSITION = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	}
}

void fragment() {
	vec4 t = texture(tex, UV);
	float life = clamp(life_phase, 0.0, 1.0);
	vec4 prm = texture(prm_ramp, vec2(life, 0.5));
	vec3 env = texture(env_ramp, vec2(life, 0.5)).rgb;
	ALBEDO = to_linear(flat_prm ? prm.rgb : mix(env, prm.rgb, t.r));
	ALPHA = t.a * prm.a * COLOR.a;
}
