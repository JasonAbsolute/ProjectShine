// ms_m_awahamon (BSP1): Direction shape along the rise (a flat quad on the surface), blend SRCALPHA + ONE, colour prm flat, alpha = TEXA x prmA
// x the ESP1 alpha (COLOR.a); the texture matrix starts at 1.4999 and shrinks 0.0099 a frame (INSTANCE_CUSTOM.x), so the ring grows
// from 2/3 to 5/6 of the 45-unit quad over its 30 frames. Outside the texture: nothing (an I8 ring on black).
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled;

uniform sampler2D tex : source_color, filter_linear, repeat_disable;
uniform vec3 prm_color : source_color = vec3(1.0, 1.0, 1.0);

varying float uv_scale;

void vertex() {
	uv_scale = INSTANCE_CUSTOM.x; // per-instance data is read in the vertex stage
}

void fragment() {
	float s = uv_scale;
	vec2 uv = (UV - 0.5) * s + 0.5;
	float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
	float a = texture(tex, clamp(uv, 0.0, 1.0)).r * inside;
	ALBEDO = prm_color;
	ALPHA = a * COLOR.a;
}
