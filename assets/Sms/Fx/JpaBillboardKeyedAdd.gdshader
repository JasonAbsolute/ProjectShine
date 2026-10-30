// A JPA Billboard (plain camera-facing quad, the particle's angle about the view axis, each side scaled by the particle's scale) whose colour
// tables run by the particle's AGE: colour = lerp(env(age), prm(age), I) of the raw I / IA texture, both tables as 1D ramps sampled by the
// particle's life phase (INSTANCE_CUSTOM.y), alpha = A x COLOR.a (the ESP1 fade in the process material's colour ramp). Blend SRCALPHA / ONE
// (additive), z test on, no write. Pass 130b: ms_bomb_bomb, the Bob-omb blast's glow ball (prm (255,231,207) -> red, env (255,159,72) -> black
// over its first 16 frames). GX colours are display-referred: the mix is done raw and the result put to linear.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear, repeat_disable;          // raw I / IA: r = intensity, a = alpha (= I for an I texture)
uniform sampler2D prm_ramp : filter_linear, repeat_disable;     // colorPrm over the life (a GradientTexture1D), raw 8-bit / 255 like the other jpa shaders' prm
uniform sampler2D env_ramp : filter_linear, repeat_disable;     // colorEnv over the life

varying float phase;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	phase = INSTANCE_CUSTOM.y;
	float a = INSTANCE_CUSTOM.x;
	mat4 world = mat4(normalize(INV_VIEW_MATRIX[0]), normalize(INV_VIEW_MATRIX[1]), normalize(INV_VIEW_MATRIX[2]), MODEL_MATRIX[3]);
	world = world * mat4(vec4(cos(a), -sin(a), 0.0, 0.0), vec4(sin(a), cos(a), 0.0, 0.0), vec4(0.0, 0.0, 1.0, 0.0), vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_MATRIX = VIEW_MATRIX * world;
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0), vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0), vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0), vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	vec4 t = texture(tex, UV);
	vec3 prm = texture(prm_ramp, vec2(phase, 0.5)).rgb;
	vec3 env = texture(env_ramp, vec2(phase, 0.5)).rgb;
	ALBEDO = to_linear(mix(env, prm, t.r));
	ALPHA = t.a * COLOR.a;
}
