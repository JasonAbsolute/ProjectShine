shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, skip_vertex_transform, fog_disabled;

// SMS 237: the Secret of Ricco Tower's sky -- coro_ex2/map/map/sky.bmd's one material, _VRsph, drawn as the game draws it (Map/Sky.cpp TSky: the dome
// rides with the camera, unlit, added over black). The TEV: glow texture (P_casino_glow2mm, texgen 0: scale 10, offset (0, -0.06)) x C0 x the 8-bit
// Mario pattern (B_crasicmario, texgen 1); the blend ONE + ONE. sky.btk (600 frames, looping) drives the pattern's texture matrix: scale 1.25 for
// frames 0-299 then 2.5, and a translation stepping 1/16 every 37.5 frames; sky.brk drives C0: white for frames 0-299, (100, 255, 255) after.
// The maths stays in the game's gamma space; the result is linearised at the end so the screen shows the game's numbers.
// Knobs default to the game's; speed scales the clock, brightness the whole sky.

uniform sampler2D glow_tex : filter_linear_mipmap, repeat_enable;      // P_casino_glow2mm (I8)
uniform sampler2D pattern_tex : filter_linear_mipmap, repeat_enable;   // B_crasicmario (RGB565)
uniform vec2 glow_scale = vec2(10.0, 10.0);
uniform vec2 glow_offset = vec2(0.0, -0.06);
uniform float frame_rate = 30.0;
uniform float speed = 1.0;
uniform float loop_frames = 600.0;
uniform float switch_frame = 300.0;
uniform float pattern_scale_a = 1.25;
uniform float pattern_scale_b = 2.5;
uniform float step_frames = 37.5;
uniform float step_size = 0.0625;
uniform vec3 tint_a = vec3(1.0, 1.0, 1.0);
uniform vec3 tint_b = vec3(0.392157, 1.0, 1.0);
uniform float brightness = 1.0;

varying vec2 v_uv;

void vertex() {
	// the dome centred on the camera: the model's rotation and scale kept, its placement dropped (TSky::perform: the base matrix is the camera's position)
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz - MODEL_MATRIX[3].xyz + CAMERA_POSITION_WORLD;
	VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
	NORMAL = normalize((VIEW_MATRIX * vec4((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz, 0.0)).xyz);
	v_uv = UV;
}

void fragment() {
	float frame = mod(TIME * frame_rate * speed, loop_frames);
	bool second = frame >= switch_frame;
	float s = second ? pattern_scale_b : pattern_scale_a;
	float t = min(floor((frame + 0.5) / step_frames), loop_frames / step_frames - 1.0) * step_size;
	vec2 uv0 = glow_scale * (v_uv - 0.5) + 0.5 + glow_offset;          // J3D: S * (uv - centre) + centre + T
	vec2 uv1 = vec2(s) * (v_uv - 0.5) + 0.5 + vec2(t, t);
	vec3 glow = texture(glow_tex, uv0).rgb;
	vec3 pat = texture(pattern_tex, uv1).rgb;
	vec3 col = glow * (second ? tint_b : tint_a) * pat;
	ALBEDO = pow(clamp(col * brightness, 0.0, 1.0), vec3(2.2));
	ALPHA = 1.0;
}
