// The episode select's EMPTY (open, not cleared) shine body -- shine_menu_empty.bmd '_mat_shine_body' as read (bmd_mat_dump.py; SMS pass
// 127d): the translucent blue glass star. Two radgrad lookups by the view-space normal: tex0 (texmtx0 scale 0.454: the gradient spread wide, so
// I0 is high over most of the face and drops only at the rim), tex1 (scale 2.03: a central spot). The TEV, raw 8-bit / 255:
//   stage 0  colour = (lerp(K0 (0, 15, 255), 0, I0) + C2 (149, 149, 141) - 0.5) x 4, clamped  -- grey-blue, going light BLUE where I0 falls (the rim)
//            alpha  = lerp(0.75, 0, I0) + A2 (140 / 255)                                           -- 0.55 at the face, 1 at the rim
//   stage 1  colour += K1 (143, 141, 128) x I1                                                     -- a soft grey highlight at the centre
//            alpha  += 0.25 x I1
//   stage 2  alpha  x= RASA (the material alpha: the bpk's fade)
// Blend SRCALPHA / INVSRCALPHA, z tested, NOT written (translucent). The colour needs no light (RASC is unused), so unshaded.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D radgrad : filter_linear, repeat_disable;   // P_ms_radgrad_i (raw: r = I)
uniform vec3 konst0 = vec3(0.0, 0.0588, 1.0);               // (0, 15, 255)
uniform vec3 konst1 = vec3(0.5608, 0.5529, 0.5020);         // (143, 141, 128)
uniform vec4 c2 = vec4(0.5843, 0.5843, 0.5529, 0.5490);     // (149, 149, 141, 140)
uniform vec2 scale0 = vec2(0.454102, 0.454102);
uniform vec2 scale1 = vec2(2.02637, 2.02637);
uniform float fade = 1.0;                                   // the body's alpha: SmsSelectShineRing's FrontAlpha x the bpk's depth fade

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
	vec3 n = normalize(NORMAL);                             // view space: the glass look follows the view, as in the game
	vec2 nuv = n.xy * 0.5 + 0.5;
	float i0 = texture(radgrad, (nuv - 0.5) * scale0 + 0.5).r;
	float i1 = texture(radgrad, (nuv - 0.5) * scale1 + 0.5).r;
	vec3 col = clamp((mix(konst0, vec3(0.0), i0) + c2.rgb - 0.5) * 4.0, 0.0, 1.0);
	float a = clamp(mix(0.75, 0.0, i0) + c2.a, 0.0, 1.0);
	col = clamp(col + konst1 * i1, 0.0, 1.0);
	a = clamp(a + 0.25 * i1, 0.0, 1.0);
	ALBEDO = to_linear(col);
	ALPHA = a * fade;
}
