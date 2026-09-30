// The additive twin of stripe_blur.gdshader for ms_m_sphipd_c (the super hip drop's fire streak, see SmsSuperHipDropBlur.cs): the game blends it SRCALPHA + ONE: the blur texture (repeat) scaled by the
// BSP1 texture matrix (1.75 across, 0.1 along) and scrolled 0.2 a particle frame (60 Hz: the JPA manager is calc'd twice a rendered frame) along the ribbon, shaped by the second texture
// (the mask, an indirect stage in the game) sampled with the ribbon's own UV: u across the ribbon, v from the head (0) to the tail (1).
shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never, shadows_disabled;

uniform sampler2D blur_tex : source_color, repeat_enable, filter_linear;
uniform sampler2D mask_tex : source_color, repeat_disable, filter_linear;
uniform vec2 tex_scale = vec2(1.75, 0.1);
uniform float scroll_per_frame = 0.2;
uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
	vec2 uv = UV * tex_scale + vec2(0.0, TIME * scroll_per_frame * 60.0);
	vec4 c = texture(blur_tex, uv);
	float m = texture(mask_tex, UV).a;
	ALBEDO = c.rgb * tint.rgb * COLOR.rgb;
	ALPHA = c.a * m * tint.a * COLOR.a;
}
