// A JPA DirBillboard (dirType Vel) for GPUParticles3D quads: a camera-facing quad whose Y runs along the particle's velocity as seen on
// screen (JPADrawVisitor's DirBillboard: the view-space velocity with its z dropped), X across it, each side scaled by the particle's own
// axis scale -- the process material's scale curve lands in the transform's columns, and particle_flag_align_y puts the velocity in the
// Y column. Colour as JPA's colour in-select 0: lerp(env, prm, I) of an IA texture; alpha = A x prm.a x COLOR.a. The env colour is a
// constant or a colour table cycling over the particle's life from a random phase (JEFFjpa1 colour calc flag bit 0: the loop offset;
// INSTANCE_CUSTOM.z = the process material's anim_offset, random per particle when anim_offset_min / max span 0..1). GX colours are
// display-referred: the mix is done raw and the result put to linear. The GX alpha compare is the alpha scissor (alpha_ref).
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, unshaded, fog_disabled;

uniform sampler2D tex : filter_linear, repeat_disable;          // IA: r = intensity, a = alpha
uniform vec4 prm = vec4(1.0, 1.0, 1.0, 1.0);                    // colorPrm, raw 8-bit / 255
uniform vec4 env = vec4(0.0, 0.0, 0.0, 1.0);                    // colorEnv when not animated
uniform bool env_animated = false;
uniform sampler2D env_ramp : filter_linear, repeat_enable;      // the env colour table over one cycle
uniform float env_cycles = 1.0;                                 // the life in frames / the table's frames
uniform float alpha_ref = 0.5;                                  // the GX alpha compare reference (GEQUAL)

varying vec3 v_env;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	vec3 center_v = (VIEW_MATRIX * MODEL_MATRIX[3]).xyz;
	vec3 vel_w = MODEL_MATRIX[1].xyz;                           // align_y: the Y column is the velocity's direction x the Y scale
	float sx = length(MODEL_MATRIX[0].xyz);
	float sy = length(vel_w);
	vec3 vel_v = (VIEW_MATRIX * vec4(vel_w, 0.0)).xyz;
	vec2 up = length(vel_v.xy) > 1e-6 ? normalize(vel_v.xy) : vec2(0.0, 1.0);
	vec2 right = vec2(up.y, -up.x);
	vec3 p = center_v + vec3(right * (VERTEX.x * sx) + up * (VERTEX.y * sy), 0.0);
	POSITION = PROJECTION_MATRIX * vec4(p, 1.0);
	v_env = env_animated ? texture(env_ramp, vec2(fract(INSTANCE_CUSTOM.y * env_cycles + INSTANCE_CUSTOM.z), 0.5)).rgb : env.rgb;
}

void fragment() {
	vec4 t = texture(tex, UV);
	ALBEDO = to_linear(mix(v_env, prm.rgb, t.r));
	ALPHA = t.a * prm.a * COLOR.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_ref;
}
