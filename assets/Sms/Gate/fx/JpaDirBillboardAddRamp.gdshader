// JpaDirBillboardAdd.gdshader with the particle's COLOR as the prm colour (SMS pass 127c): for an emitter whose prm colour is KEYED over the
// particle's age (ms_2d_shine_senko / kira_em: blue -> green -> red every 30 frames) the ramp carries the key table in COLOR.rgb (raw 8-bit / 255,
// display-space) and the shader does the TEV: colour = lerp(env, prm, I) x COLOR.rgb -- with env black that is 'TEXC x prm' --, alpha = A x
// prm.a x COLOR.a (the ESP1 fade). Blend SRCALPHA / ONE, z test on / no write. The quad: camera-facing, its Y along the particle's velocity
// on screen (a JPA 'Direction' billboard), each side scaled by the particle's axis scale, the base at the particle (pivot Y 2: center_offset).
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear, repeat_disable;          // raw I / IA: r = intensity, a = alpha (= I for an I texture)
uniform vec4 prm = vec4(1.0, 1.0, 1.0, 1.0);                    // the colour the ramp multiplies (white: the ramp IS prm)
uniform vec4 env = vec4(0.0, 0.0, 0.0, 1.0);                    // black: colour = TEXC x prm (the senko's mode); white: lerp(white -> prm, I)

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
	ALBEDO = to_linear(mix(env.rgb, prm.rgb, t.r) * COLOR.rgb);
	ALPHA = t.a * prm.a * COLOR.a;
}
