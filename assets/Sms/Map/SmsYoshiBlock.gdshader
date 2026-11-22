shader_type spatial;
render_mode cull_back, depth_draw_opaque;

// The Yoshi block's look (yoshiblock.bmd, material _o1, TEV stage 0): the egg-pattern sheet B_eggPatarn2 is an INTENSITY texture, and the
// stage COMPARES it with the block's tint instead of multiplying: GX_TEV_COMP_BGR24_GT with a = TEXC, b = C0 (the tint TWoodBlock::load puts in
// GX_TEVREG0), c = TEXC, d = C0 gives  out = C0 + (tex.bgr24 > C0.bgr24 ? tex : 0), clamped. So the white body of the sheet stays WHITE and only
// the dark spots take the tint (slightly lightened by the spot's own grey). Stage 1 multiplies the lit vertex colour in, which Godot's lighting does.
// Pass 243; the game's blocks are white with coloured spots (MarioWiki's Yoshi-Go-Round screenshot), not a tinted grey.
// 247: the sheet is sampled CLAMPED (yoshiblock.bmd's texture header: wrapS 0, wrapT 0). The mesh's UVs run from -1.4 to 1 and the pattern is
// built from that: polygons sent below v = 0 land on the sheet's top row (inside the spot: the tint), polygons sent left of u = 0 land on its
// left column (white), and only the polygons inside 0..1 see the spot's soft edge. 243-246 sampled with repeat, which tiled the quarter-spot
// over those polygons and cut it into crescents ("corrupted").
uniform sampler2D egg_tex : filter_linear_mipmap, repeat_disable;
uniform vec4 tint : source_color = vec4(1.0);
uniform float brightness = 1.0;

float bgr24(vec3 c) {
	// the comparison the TEV makes: the colour as a 24-bit integer with blue in the high byte
	vec3 b = floor(clamp(c, 0.0, 1.0) * 255.0 + 0.5);
	return b.b * 65536.0 + b.g * 256.0 + b.r;
}

void fragment() {
	vec4 t = texture(egg_tex, UV);
	vec3 tex = t.rgb;                      // an I8 sheet: r = g = b = the intensity
	vec3 c0 = tint.rgb;
	vec3 col = c0 + (bgr24(tex) > bgr24(c0) ? tex : vec3(0.0));
	col = clamp(col * brightness, 0.0, 1.0);
	ALBEDO = pow(col, vec3(2.2));          // the game's colour values are display values: to linear, as the sky ports do, so the screen shows the game's tint
	ROUGHNESS = 1.0;
	METALLIC = 0.0;
	SPECULAR = 0.0;
}
