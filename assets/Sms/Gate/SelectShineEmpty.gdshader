// The episode select's EMPTY (open, not cleared) shine body -- shine_menu_empty.bmd '_mat_shine_body' (bmd_mat_dump.py; SMS pass 127d), with
// 127h's default look in front of it.
//
// 127h DEFAULT (game_look false, DEVIATION -- Jaysonn: "the tint as 1 at every angle", his screenshots 1 / 2): a glass of ONE colour --
// `glass` on the flat faces, `rim` on the bevels and side walls, picked by each normal's tilt off the star's own face axis in the MESH (so the
// front and the back of the star match, and nothing follows the camera) -- drawn as a SINGLE layer: SmsSelectShineRing puts a depth-only pass
// (priority -2, writes depth, draws nothing) in front of this material (its next_pass, -1), so edge-on the side walls no longer stack their
// alpha into a solid bar, and the glow behind the body (priority 0, depth-tested) stays behind it. (Godot's own depth_prepass_alpha writes
// depth only for near-opaque fragments, so it cannot do this for a 60 % glass.)
// 127g's fold (every side wall sent to the lookup's CENTRE) painted the walls the TEV's cream highlight -- the cream stripes in the 127g
// edge-on capture, the colour change he saw each turn.
//
// game_look true: the bmd's TEV as read. Two radgrad lookups by the view-space normal: tex0 (texmtx0 scale 0.454: the gradient spread wide,
// so I0 is high over most of the face and drops only at the rim), tex1 (scale 2.03: a central spot). Raw 8-bit / 255:
//   stage 0  colour = (lerp(K0 (0, 15, 255), 0, I0) + C2 (149, 149, 141) - 0.5) x 4, clamped  -- grey-blue, going light BLUE where I0 falls (the rim)
//            alpha  = lerp(0.75, 0, I0) + A2 (140 / 255)                                           -- 0.55 at the face, 1 at the rim
//   stage 1  colour += K1 (143, 141, 128) x I1                                                     -- a soft cream highlight at the centre
//            alpha  += 0.25 x I1
//   stage 2  alpha  x= RASA (the material alpha: the bpk's fade)
// Blend SRCALPHA / INVSRCALPHA, z tested, NOT written (translucent). The colour needs no light (RASC is unused), so unshaded. Its two
// lookups are by the VIEW-space normal in the game, so as the star spins toward edge-on every visible face becomes 'rim' and the whole shine
// turns opaque light blue; `rim_follows_view` false takes the normal's angle against the star's own face axis instead (127g).
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled, fog_disabled;

uniform bool game_look = false;                             // true = the bmd's TEV (colour changes with the spin); false = the one-colour glass
uniform vec4 glass : source_color = vec4(0.557, 0.541, 0.733, 0.6);   // the flat faces (SmsSelectShineRing.EmptyTint)
uniform vec4 rim : source_color = vec4(0.79, 0.79, 0.74, 0.75);       // the bevels and side walls (SmsSelectShineRing.EmptyRim)
uniform float rim_start = 0.45;                             // the tilt (sine of the normal's angle off the face) where the rim colour begins
uniform float rim_end = 0.8;                                // ... and where it is full
uniform sampler2D radgrad : filter_linear, repeat_disable;   // P_ms_radgrad_i (raw: r = I) -- the game look
uniform vec3 konst0 = vec3(0.0, 0.0588, 1.0);               // (0, 15, 255)
uniform vec3 konst1 = vec3(0.5608, 0.5529, 0.5020);         // (143, 141, 128)
uniform vec4 c2 = vec4(0.5843, 0.5843, 0.5529, 0.5490);     // (149, 149, 141, 140)
uniform vec2 scale0 = vec2(0.454102, 0.454102);
uniform vec2 scale1 = vec2(2.02637, 2.02637);
uniform float fade = 1.0;                                   // the body's alpha: SmsSelectShineRing's FrontAlpha x the bpk's depth fade
uniform bool rim_follows_view = false;                      // game look: true = the game's view-space lookup (the rim swings with the spin)
uniform vec3 face_axis = vec3(0.0, 0.0, 1.0);               // the star's face normal in the mesh's own space (SmsSelectShineRing sets it)
uniform float rim_fold = 0.9;                               // game look, locked mode: a normal tilted past this folds back toward the face tint

varying vec3 n_model;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	n_model = NORMAL;                                       // the mesh's object space, before the spin
}

void fragment() {
	vec3 nm = normalize(n_model);
	float along = dot(nm, normalize(face_axis));
	float tilt = sqrt(max(0.0, 1.0 - along * along));      // 0 on the flat faces (front OR back), 1 on the side walls
	if (!game_look) {
		float w = smoothstep(rim_start, rim_end, tilt);
		vec4 c = mix(glass, rim, w);                        // source_color: already linear
		ALBEDO = c.rgb;
		ALPHA = c.a * fade;
	} else {
		vec2 nuv;
		if (rim_follows_view) {
			vec3 n = normalize(NORMAL);                     // view space: the glass look follows the view, as in the game
			nuv = n.xy * 0.5 + 0.5;
		} else {
			float r = tilt;
			if (r > rim_fold) {
				r = rim_fold * (1.0 - r) / max(1.0 - rim_fold, 0.0001);
			}
			nuv = vec2(0.5 + 0.5 * r, 0.5);                 // the radgrad is radial: one axis carries the whole lookup
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
}
