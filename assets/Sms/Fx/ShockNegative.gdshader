// The shock's negative -- TMario::drawLogic() (MarioDraw.cpp 2075): during MARIO_STATUS_ELECTRIC_DAMAGE the body model is drawn a
// second time with GXSetBlendMode(GX_BM_LOGIC, ..., mBlendLogicOp = 10 = GX_LO_INV) (MarioInit.cpp 420): every pixel it covers becomes
// its own inverse (a colour-off depth pass first, so a pixel inverts once). Here the material overlay of Mario's meshes: reading the
// screen puts it in the transparent pass, over the shaded body, at the body's own depth -- 1 - screen at each pixel, once.
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never;

uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_nearest;

void fragment() {
	vec3 seen = texture(screen_tex, SCREEN_UV).rgb;
	ALBEDO = vec3(1.0) - seen;
}
