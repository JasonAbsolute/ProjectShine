// The episode select's FULL shine body -- shine_menu.bmd '_mat_shine_body' as read (bmd_mat_dump.py; SMS pass 127d). GX: matColor (225, 120, 30)
// lit by the channel (ambient (128, 128, 128) + one light's N.L), then two radgrad lookups by the VIEW-SPACE NORMAL (texgen src NRM, texmtx type
// 0x06 'envmap old'): stage 0 adds konst (255, 196, 155) x radgrad(texmtx0: scale 1.528, trans -0.15) -- a broad cream highlight where the
// normal faces the view --, stage 1 adds radgrad(texmtx1: scale 4.09, trans 0.908) -- a small white spot; alpha = RASA = the material alpha
// (the bpk's depth fade). Blend SRCALPHA / INVSRCALPHA, z tested AND written (a translucent star that occludes itself). EVERY sum is done in
// DISPLAY space, as the GX adds 8-bit values; summed in linear space (Godot's lighting + EMISSION) the same values gave a dull copper.
//
// 127f, fitted to the game's pixels (ref/game_select.png, px_probe.py: the body (248, 248, 88) lemon yellow over the whole star, a cream
// (248, 248, 170) highlight region, gold bevels) -- the select scene sets no light of its own (SelectDir.cpp), so the light's strength is not
// in the data: (1) the lit term alone must reach G 232+ over the body, which the orange (225, 120, 30) only does at 0.5 + 2.0 x N.L, so the
// light is 2.0 x white (BodyLight; 1.0 gave an orange body with cream patches, 1.6 a gold lower dome); (2) the stage-0 sheen's translate is applied BEFORE its scale
// -- uv = scale x (n - 0.5 + trans) + 0.5 -- putting a face-on normal at radgrad 0.41 (the two conventions are one matrix with trans' = scale
// x trans, so trans0 is the disc's -0.15 x 1.528); 127d's after-scale reading sat the face at 0.76 and painted it pink-cream. Stage 1 keeps
// the after-scale reading (before-scale it clamps off the texture and draws nothing). The light stays FIXED TO THE SCREEN as in the game:
// whichever side of the spinning star faces the viewer is lit; a try at turning the light with the spin lit the front only and the back of
// every half-turn went dark brown.
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D radgrad : filter_linear, repeat_disable;             // P_ms_radgrad_i (raw: r = I)
uniform vec4 mat_color = vec4(0.8824, 0.4706, 0.1176, 1.0);          // (225, 120, 30)
uniform float ambient = 0.5;                                          // ambColor0 (128, 128, 128)
uniform vec3 light_dir = vec3(0.15, 0.35, 0.92);                      // toward the light, view space: nearly frontal, a little up-right
uniform float light_strength = 2.0;                                   // the light's colour (white x this): 2.0 fits the game's yellow (see above)
uniform vec4 konst0 = vec4(1.0, 0.7686, 0.6078, 1.0);                 // (255, 196, 155): the highlight's colour
uniform vec2 scale0 = vec2(1.52832, 1.52832);
uniform vec2 trans0 = vec2(-0.2292, -0.2292);                         // the disc's -0.15 before the 1.528 scale (see above); -0.15 = 127d's after-scale reading
uniform vec2 scale1 = vec2(4.0918, 4.0918);
uniform vec2 trans1 = vec2(0.908203, 0.908203);
uniform float highlight = 1.0;                                        // x the two highlights (1 = the game)
uniform float fade = 1.0;                                             // the body's alpha: SmsSelectShineRing's FrontAlpha x the bpk's depth fade

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
	vec3 n = normalize(NORMAL);                                        // view space
	float ndl = max(dot(n, normalize(light_dir)), 0.0);
	vec3 lit = clamp(mat_color.rgb * (ambient + ndl * light_strength), 0.0, 1.0);   // GX_DF_CLAMP diffuse + ambient, display space
	vec2 nuv = n.xy * 0.5 + 0.5;                                       // the normal's x / y as a texcoord (GX_TG_NRM)
	vec2 uv0 = (nuv - 0.5) * scale0 + 0.5 + trans0;
	vec2 uv1 = (nuv - 0.5) * scale1 + 0.5 + trans1;
	float i0 = texture(radgrad, uv0).r;
	float i1 = texture(radgrad, uv1).r;
	vec3 col = clamp(lit + (konst0.rgb * i0 + vec3(i1)) * highlight, 0.0, 1.0);
	ALBEDO = to_linear(col);
	ALPHA = fade;
}
