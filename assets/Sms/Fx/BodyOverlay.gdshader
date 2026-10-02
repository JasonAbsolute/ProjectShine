// The body's overlay -- two things the game draws over Mario's own pixels, in its order:
//  1. the damage fog, TMario::addDamageFog (MarioDraw.cpp 2185) while mInvincibilityFrames > 0 and not talking: every material of the
//     body, cap and hands gets a fog of colour (0xFF, 0x00, 0x80) whose range SMS_AddDamageFogEffect (DrawUtil.cpp) sets about his
//     view depth d: start = d - 700 + 300 s, end = d + 500 + 300 s, s = sin(moveTick x 0x888) (a 30-tick, one-second pulse) -- at his
//     depth the linear fog factor is (700 - 300 s) / 1200 = 7/12 - s/4, 0.33..0.83 toward the pink;
//  2. the shock's negative, TMario::drawLogic (MarioDraw.cpp 2075) during ELECTRIC_DAMAGE: the body drawn again with GX_BM_LOGIC and
//     mBlendLogicOp 10 = GX_LO_INV, every covered pixel its own inverse, after a colour-off depth pass so each inverts once.
// Both happen in the GameCube's 8-bit framebuffer, i.e. in gamma space; Godot's screen buffer is linear light, so the colour is taken
// to sRGB, blended / inverted there, and brought back -- in linear light a third of pink is already a heavy magenta and the pulse
// hardly shows. Reading the screen puts this material in the transparent pass, over the shaded body, at the body's own depth.
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never;

uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_nearest;
uniform float fog = 0.0;
uniform vec3 fog_color = vec3(1.0, 0.0, 0.5); // (0xFF, 0x00, 0x80), a framebuffer (gamma-space) colour
uniform bool negative = false;

vec3 to_gamma(vec3 c) {
	c = clamp(c, vec3(0.0), vec3(1.0));
	return mix(12.92 * c, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(vec3(0.0031308), c));
}

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void fragment() {
	vec3 c = to_gamma(texture(screen_tex, SCREEN_UV).rgb);
	c = mix(c, fog_color, fog);
	if (negative) {
		c = vec3(1.0) - c;
	}
	ALBEDO = to_linear(c);
}
