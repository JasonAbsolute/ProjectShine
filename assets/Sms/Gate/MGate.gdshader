// The M gate -- 05_gate01.bmd's one material, _mat_gate1 (SMS pass 127; the bmd's MAT3 dumped with tools/projectshine/bmd_mat_dump.py).
// A flat 400 x 400-unit quad. The game's TEV: stages 0-3 turn the THP movie's Y / U / V planes (three dummy textures the player swaps
// in each frame) into RGB; stage 4 reads the rainbow sheen (P_gate_spec2s3tc, texmtx3 scrolled by the btk) x (1 - K3.g); stage 5 the
// M mask (P_gate_msk_m2, texmtx5 scaled about the centre) -> C1 = mask x K3.r, A1 = (mask - K3.r) x 4; stage 6 mixes the dim sheen
// (C2) toward the movie by A1 and sets alpha = mask x the material alpha x 2. K3 is the brk's register: (255, 130) at glow 0 ->
// (91, 0) at glow 1 (frame 99), so a sleeping gate is the M in a dim sheen and an active one opens its window on the movie as the sheen
// brightens. The indirect stage (P_ms_indwp1_ia via texmtx4: the btk breathes its scale 2 -> 1 -> 2 every 8 s and circles its offset
// once in 40 s) wobbles the sheen and the M's edge (stages 4-6, mtx 0.6 x 2^-5, bias -128), never the movie. Blend SRCALPHA / ZERO
// with alpha compare >= 80: opaque inside the M, cut outside. The bpk (startOpen) raises the material alpha 12 -> 255 over 100 frames.
// A still (preview_tex) stands in for the movie -- DEVIATION; with none the window opens on the full-bright sheen instead of black.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque, shadows_disabled;

// -- textures ------------------------------------------------------------------------------------------------------------------------
uniform sampler2D mask_tex : filter_linear, repeat_disable;             // The M shape (P_gate_msk_m2, 64 x 64; intensity = coverage).
uniform sampler2D sheen_tex : source_color, filter_linear, repeat_enable; // The rainbow sheen (P_gate_spec2s3tc, 32 x 32).
uniform sampler2D warp_tex : filter_linear, repeat_enable;              // The wobble map (P_ms_indwp1_ia, 32 x 32: alpha = S, intensity = T).
uniform sampler2D preview_tex : source_color, filter_linear, repeat_disable; // The picture inside the M (the game: the stage's movie).
uniform bool has_preview = false;                                       // Off: the open window shows the bright sheen.
// -- state (the gate's script drives these) ------------------------------------------------------------------------------------------
uniform float glow : hint_range(0.0, 1.0) = 0.0;        // unkD0: 0 = asleep (dim sheen), 1 = active (bright, the window open). The brk frame / 99.
uniform float open_alpha : hint_range(0.0, 1.0) = 1.0;  // The bpk's material alpha: 12/255 at the start of the open fade, 1 when open.
// -- knobs (the game's values as defaults) --------------------------------------------------------------------------------------------
uniform float mask_scale = 0.91;          // texmtx5: the M drawn this much of the quad (gate01 0.91, rico 0.99, manma 0.95).
uniform float sheen_scroll_seconds = 40.0; // The btk's 1200-frame diagonal scroll of the sheen (uv 0 -> 1).
uniform float warp_circle_seconds = 40.0;  // The btk's 1200-frame circle of the wobble map's offset.
uniform float warp_breathe_seconds = 8.0;  // The btk's 240-frame breath of the wobble map's scale (2 -> 1 -> 2).
uniform float warp_strength = 1.0;         // x the game's wobble (0.6 x 2^-5 x 255 texels): 0 = none.
uniform vec2 preview_scale = vec2(1.0);    // The picture's fit inside the quad (1 = edge to edge).
uniform vec2 preview_offset = vec2(0.0);
uniform float alpha_cut = 0.3137;          // GX alpha compare GEQUAL 80 / 255: below this the pixel is cut.
uniform float brightness = 1.0;            // DEVIATION: a plain multiplier on the result (the game: 1).

void fragment() {
	vec2 uv = UV;
	float t = TIME;
	// texmtx4 (the indirect lookup): scale breathing 2 -> 1 -> 2, the offset circling (cos, sin) once a period, about the centre.
	float breathe = 1.5 + 0.5 * cos(TAU * t / warp_breathe_seconds);
	float ang = TAU * t / warp_circle_seconds;
	vec2 wuv = (uv - 0.5) * breathe + 0.5 + vec2(cos(ang), sin(ang));
	vec4 w = texture(warp_tex, wuv);
	vec2 ind = (vec2(w.a, w.r) - 0.5) * 255.0 * 0.6 / 32.0 * warp_strength;   // texels of the stage's own texture
	// stage 4: the sheen, texmtx3 scrolling (t, t) over the scroll time, wobbled
	vec2 suv = uv + vec2(t / sheen_scroll_seconds) + ind / 32.0;
	vec3 sheen = texture(sheen_tex, suv).rgb;
	float k3r = mix(1.0, 91.0 / 255.0, glow);
	float k3g = mix(130.0 / 255.0, 0.0, glow);
	vec3 c2 = sheen * (1.0 - k3g);
	// stages 5-6: the mask about the centre, wobbled
	vec2 muv = (uv - 0.5) * mask_scale + 0.5 + ind / 64.0;
	float m = texture(mask_tex, muv).r;
	float a1 = clamp((m - k3r) * 4.0, 0.0, 1.0);
	vec3 window = has_preview ? texture(preview_tex, (uv - 0.5) * preview_scale + 0.5 + preview_offset).rgb : sheen;
	vec3 col = mix(c2, window, a1);
	float alpha = clamp(m * open_alpha * 2.0, 0.0, 1.0);
	if (alpha < alpha_cut) {
		discard;
	}
	ALBEDO = col * alpha * brightness;   // blend SRCALPHA / ZERO: the colour by its alpha over nothing
}
