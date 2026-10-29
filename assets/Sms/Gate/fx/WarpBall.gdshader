// TMario::warpInEffect's light balls (SMS pass 127c; SmsWarpBalls.cs drives a MultiMesh of these): ms_mariowp_body / head / cap / hands / legs
// / feet / watgun -- a 20-unit billboard of P_msm_mizutama1 (IA8: I = the drop's shading, bright core / dark rim; A = the disc). The TEV is
// colour = lerp(env, prm, I): the RIM is env (the part's deep colour) and the CORE prm (its light colour) -- a vivid two-tone sphere, never a
// grey marble (127b multiplied one flat colour into the grey shading). Alpha = A, compared GEQUAL 222 (a hard disc), blend SRCALPHA /
// INVSRCALPHA, z tested and written. prm rides the instance COLOR, env the instance custom data; both raw 8-bit / 255 (display-space), the
// mix is done raw and put to linear. The quad faces the camera and keeps its instance scale (the StandardMaterial3D billboard-keep-scale form).
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear_mipmap, repeat_disable;   // mizutama1_ia (raw: r = I, a = A)
uniform float alpha_ref = 0.8706;                               // 222 / 255
varying vec3 env_v;                                             // the instance's custom data (env) -- a vertex-stage built-in, carried over

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	env_v = INSTANCE_CUSTOM.rgb;
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0), vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0), vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0), vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	vec4 t = texture(tex, UV);
	ALBEDO = to_linear(mix(env_v, COLOR.rgb, t.r));
	ALPHA = t.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_ref;
}
