// The body's overlay -- the shock's negative, TMario::drawSpecial -> drawLogic (MarioDraw.cpp 2058-2108) during MARIO_STATUS_ELECTRIC_DAMAGE:
// the body drawn again with GX_BM_LOGIC and mBlendLogicOp 10 = GX_LO_INV, every covered pixel its own inverse, after a colour-off depth pass
// so each inverts once. It happens in the GameCube's 8-bit framebuffer, i.e. in gamma space; Godot's screen buffer is linear light, so the
// colour is taken to sRGB, inverted there, and brought back. Reading the screen puts this material in the transparent pass, over the shaded
// body, at the body's own depth -- the right place for a second pass over the finished body, which is what drawLogic is.
// The damage fog (addDamageFog) used to ride this overlay too (passes 56-96); it is a fog on the body's OWN materials in the game, and since
// pass 97 it is one here as well (MarioFog.gdshader*, SmsBodyOverlay) -- an overlay in the transparent pass kept the fogged body out of
// hint_screen_texture, so every refraction (the goop ripple, the Gooble) showed an unfogged Mario.
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never;

uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_nearest;

vec3 to_gamma(vec3 c) {
	c = clamp(c, vec3(0.0), vec3(1.0));
	return mix(12.92 * c, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(vec3(0.0031308), c));
}

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void fragment() {
	vec3 c = to_gamma(texture(screen_tex, SCREEN_UV).rgb);
	ALBEDO = to_linear(vec3(1.0) - c);
}
