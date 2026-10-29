// The episode select's EMPTY (open, not cleared) shine body -- shine_menu_empty.bmd '_mat_shine_body' as read (bmd_mat_dump.py; SMS pass
// 127d): the translucent blue glass star. Two radgrad lookups by the view-space normal: tex0 (texmtx0 scale 0.454: the gradient spread wide, so
// I0 is high over most of the face and drops only at the rim), tex1 (scale 2.03: a central spot). The TEV, raw 8-bit / 255:
//   stage 0  colour = (lerp(K0 (0, 15, 255), 0, I0) + C2 (149, 149, 141) - 0.5) x 4, clamped  -- grey-blue, going light BLUE where I0 falls (the rim)
//            alpha  = lerp(0.75, 0, I0) + A2 (140 / 255)                                           -- 0.55 at the face, 1 at the rim
//   stage 1  colour += K1 (143, 141, 128) x I1                                                     -- a soft grey highlight at the centre
//            alpha  += 0.25 x I1
//   stage 2  alpha  x= RASA (the material alpha: the bpk's fade)
// Blend SRCALPHA / INVSRCALPHA, z tested, NOT written (translucent). The colour needs no light (RASC is unused), so unshaded.
// 127g: the two lookups are by the VIEW-space normal in the game, so as the star spins toward edge-on every visible face becomes 'rim' and
// the whole shine turns opaque light blue (Jaysonn's screenshot 2 against his 1: "every 180 degrees it tints very differently -- keep the tint
// of 1"). The radgrad is radially symmetric, so a lookup only needs the normal's angle to an axis; `rim_follows_view` false (the default,
// DEVIATION) takes that angle against the star's own FACE AXIS in model space -- the flat faces stay the grey-blue glass and only the bevels
// and edges go light blue, at every spin angle. true = the game's mapping.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D radgrad : filter_linear, repeat_disable;   // P_ms_radgrad_i (raw: r = I)
uniform vec3 konst0 = vec3(0.0, 0.0588, 1.0);               // (0, 15, 255)
uniform vec3 konst1 = vec3(0.5608, 0.5529, 0.5020);         // (143, 141, 128)
uniform vec4 c2 = vec4(0.5843, 0.5843, 0.5529, 0.5490);     // (149, 149, 141, 140)
uniform vec2 scale0 = vec2(0.454102, 0.454102);
uniform vec2 scale1 = vec2(2.02637, 2.02637);
uniform float fade = 1.0;                                   // the body's alpha: SmsSelectShineRing's FrontAlpha x the bpk's depth fade
uniform bool rim_follows_view = false;                      // true = the game's view-space lookup (the rim swings with the spin)
uniform vec3 face_axis = vec3(0.0, 0.0, 1.0);               // the star's face normal in the mesh's own space (SmsSelectShineRing sets it)
uniform float rim_fold = 0.9;                               // locked mode: a normal tilted past this (0..1, the sine of its angle off the face --
                                                            // the star's extruded SIDE WALLS, hidden face-on) folds back toward the face tint, so
                                                            // edge-on the sliver is the same grey glass with blue bevel lines, not a solid blue
                                                            // bar; 1 = no fold (the side walls take the full rim blue)

varying vec3 n_model;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	n_model = NORMAL;                                       // the mesh's object space, before the spin
}

void fragment() {
	vec2 nuv;
	if (rim_follows_view) {
		vec3 n = normalize(NORMAL);                         // view space: the glass look follows the view, as in the game
		nuv = n.xy * 0.5 + 0.5;
	} else {
		vec3 n = normalize(n_model);
		float along = dot(n, normalize(face_axis));
		float r = sqrt(max(0.0, 1.0 - along * along));     // the normal's tilt off the face: 0 on the flat faces, 1 on the side walls
		if (r > rim_fold) {
			r = rim_fold * (1.0 - r) / max(1.0 - rim_fold, 0.0001);
		}
		nuv = vec2(0.5 + 0.5 * r, 0.5);                     // the radgrad is radial: one axis carries the whole lookup
	}
	float i0 = texture(radgrad, (nuv - 0.5) * scale0 + 0.5).r;
	float i1 = texture(radgrad, (nuv - 0.5) * scale1 + 0.5).r;
	vec3 col = clamp((mix(konst0, vec3(0.0), i0) + c2.rgb - 0.5) * 4.0, 0.0, 1.0);
	float a = clamp(mix(0.75, 0.0, i0) + c2.a, 0.0, 1.0);
	col = clamp(col + konst1 * i1, 0.0, 1.0);
	a = clamp(a + 0.25 * i1, 0.0, 1.0);
	ALBEDO = to_linear(col);
	ALPHA = a * fade;
}
