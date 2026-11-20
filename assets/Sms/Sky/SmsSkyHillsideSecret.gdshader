shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, skip_vertex_transform, fog_disabled;

// SMS 237: the Hillside Cave Secret's sky -- coro_ex1/map/map/sky.bmd (EXskyDarkTrain, the sky every Bianco / Ricco / Gelato secret course shares),
// drawn as the game draws it (Map/Sky.cpp TSky: the dome rides with the camera, unlit, over black). One shader, `mode` picks the material's TEV:
//   0 _o28       the dome: the sunset gradient (B_sunsetgrade, texgen 1) between C0 (12, 44, 77) and C1 (12, 20, 4), plus the stars (B_sters,
//                texgen 0, drifting one texture over the 8000-frame loop) x the vertex colour, the lot x the vertex alpha (blend SRCALPHA + ONE)
//   1 _01_Rail*  a rail silhouette (B_exsky_Rail) x its material colour, scrolling one texture over frames 0-4000 then still (ONE + INVSRCCLR -- over
//                this dark sky the same as adding)
//   2 _01_Rail_6 the train (B_exsky_Train) x the vertex colour x C0 / 2, C0 cycling blue -> orange -> green over 1000 frames (sky.brk); it crosses once
//                per loop (Tx -1.333 -> 10 over frames 0-4000) and breathes (Sy 1 -> 1.15 every 50 frames)
//   3 _cloud     the cloud band (B_exsky_Silen, repeating) x alpha 133/255, breathing (Sx 1 -> 0.9 every 50 frames) (SRCALPHA + ONE)
//   4 _plane     the aeroplane (B_exsky_Aplane) x the vertex colour x C0 (24, 73, 36), crossing once per loop (Tx -1 -> 10 over frames 0-4000) (ONE + ONE)
//   5 _silen     the ground silhouette (B_exsky_Silen, clamped) x (81, 15, 0) (ONE + ONE)
// The maths stays in the game's gamma space; the result is linearised at the end so the screen shows the game's numbers.
// Knobs default to the game's; speed scales the clock, brightness the whole sky.

uniform int mode = 0;
uniform sampler2D tex_a : filter_linear_mipmap, repeat_enable;    // the material's repeating texture (the stars, a rail, the cloud band)
uniform sampler2D tex_b : filter_linear_mipmap, repeat_disable;   // its clamped one (the gradient, the train, the plane, the ground)
uniform vec2 tex_scale = vec2(1.0, 1.0);    // texmtx0 scale: the rails' (1.25, 0.75) / (1.5, 1) / (1.75, 1); the plane's (0.5, 1)
uniform vec2 tex_center = vec2(0.5, 0.5);   // texmtx0 centre: the train's is (1, 1)
uniform vec3 mat_color = vec3(1.0, 1.0, 1.0);   // matColor0 for the REG materials; the plane's C0
uniform vec3 color_a = vec3(0.047059, 0.172549, 0.301961);   // _o28 C0
uniform vec3 color_b = vec3(0.047059, 0.078431, 0.015686);   // _o28 C1
uniform float mat_alpha = 1.0;              // the cloud's 133 / 255
uniform float frame_rate = 30.0;
uniform float speed = 1.0;
uniform float loop_frames = 8000.0;
uniform float pass_frames = 4000.0;         // the rails / train / plane: their one pass
uniform float breath_frames = 50.0;
uniform float brightness = 1.0;

varying vec2 v_uv;
varying vec2 v_uv2;
varying vec4 v_col;

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz - MODEL_MATRIX[3].xyz + CAMERA_POSITION_WORLD;
	VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
	NORMAL = normalize((VIEW_MATRIX * vec4((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz, 0.0)).xyz);
	v_uv = UV;
	v_uv2 = UV2;
	v_col = COLOR;
}

vec3 train_color(float frame) {
	// sky.brk, _01_Rail_6 C0: f0 (16, 40, 83) -> f299 (165, 45, 26) -> f599 (40, 98, 7) -> f1000 (16, 40, 83)
	float f = mod(frame, 1000.0);
	vec3 k0 = vec3(16.0, 40.0, 83.0) / 255.0;
	vec3 k1 = vec3(165.0, 45.0, 26.0) / 255.0;
	vec3 k2 = vec3(40.0, 98.0, 7.0) / 255.0;
	if (f < 299.0) return mix(k0, k1, smoothstep(0.0, 299.0, f));
	if (f < 599.0) return mix(k1, k2, smoothstep(299.0, 599.0, f));
	return mix(k2, k0, smoothstep(599.0, 1000.0, f));
}

void fragment() {
	float frame = mod(TIME * frame_rate * speed, loop_frames);
	float pass_t = min(frame / pass_frames, 1.0);
	float breath = 0.5 - 0.5 * cos(frame * TAU / breath_frames);
	vec3 col = vec3(0.0);
	if (mode == 0) {
		float grad = texture(tex_b, v_uv2).r;
		vec3 stars = texture(tex_a, v_uv + vec2(frame / loop_frames)).rgb;
		col = (clamp(grad * color_a, 0.0, 1.0) + color_b * (1.0 - grad) + v_col.rgb * stars) * v_col.a;
	} else if (mode == 1) {
		vec2 uv = tex_scale * (v_uv - tex_center) + tex_center + vec2(pass_t, 0.0);
		col = texture(tex_a, uv).rgb * mat_color;
	} else if (mode == 2) {
		vec2 uv = vec2(1.0, 1.0 + 0.15 * breath) * (v_uv - tex_center) + tex_center + vec2(mix(-1.333, 10.0, pass_t), 0.0);
		col = texture(tex_b, uv).rgb * v_col.rgb * train_color(frame) * 0.5;
	} else if (mode == 3) {
		vec2 uv = vec2(1.0 - 0.1 * breath, 1.0) * (v_uv - tex_center) + tex_center;
		vec4 t = texture(tex_a, uv);
		col = t.rgb * mat_color * (t.a * mat_alpha);
	} else if (mode == 4) {
		vec2 uv = tex_scale * (v_uv - tex_center) + tex_center + vec2(mix(-1.0, 10.0, pass_t), 0.0);
		col = texture(tex_b, uv).rgb * v_col.rgb * mat_color;
	} else {
		col = texture(tex_b, v_uv).rgb * mat_color;
	}
	ALBEDO = pow(clamp(col * brightness, 0.0, 1.0), vec3(2.2));
	ALPHA = 1.0;
}
