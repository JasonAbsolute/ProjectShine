// JpaDirBillboard.gdshader's ADDITIVE twin (blend SRCALPHA / ONE, z test on / no write): a camera-facing quad whose Y runs along the
// particle's velocity on screen, each side scaled by the particle's axis scale. Colour = lerp(env, prm, I) of the raw I / IA texture (a flat
// prm when env == prm), alpha = A x prm.a x COLOR.a (the ESP1 fade in the colour ramp). Pass 125c: ms_enm_disap_b's glow streaks.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear, repeat_disable;          // raw I / IA: r = intensity, a = alpha (= I for an I texture)
uniform vec4 prm = vec4(1.0, 1.0, 1.0, 1.0);                    // colorPrm, raw 8-bit / 255
uniform vec4 env = vec4(1.0, 1.0, 1.0, 1.0);                    // colorEnv

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
}

void fragment() {
	vec4 t = texture(tex, UV);
	ALBEDO = to_linear(mix(env.rgb, prm.rgb, t.r));
	ALPHA = t.a * prm.a * COLOR.a;
}
