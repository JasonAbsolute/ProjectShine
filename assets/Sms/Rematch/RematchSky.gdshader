// RematchSky.gdshader -- SMS 208: the King Boo rematch's SKY is Luigi's Mansion's BOSS-ARENA SKY, ported from the disc (Jaysonn: "use the skybox
// that happens in luigis mansion fights, like vs bogmire or boolossus"; the GameCube disc's /Iwamoto/vrball_B.szp, the sky of maps 10 Chauncey,
// 11 Boolossus and 13 Bogmire: vrb_mb.bmd + vrb_mb1-4.btk + vrb_mb.brk -- read with tools/projectshine/rematch157/sky208/{lm_sky_rip, lm_sky_desc,
// dome_uv}.py and drawn in numpy by lm_sky_preview.py, the same math as here).
//
// The game draws a textured dome round the camera:
//  * its texture coordinates: u = the way round the sky (one repeat), v = a curve of the ELEVATION read off the dome's vertex rings (base_v / dish_v
//    below): the base ball's v is 0 at the zenith, 0.535 at the horizon, 1 at the nadir; the streak dish's v is 0 at +74 deg, 0.31 at the horizon,
//    1 at -29 deg, and there are no streaks below that;
//  * material 'vr_o2', the BASE: colour = mix(C1, C0, bg04 x bg04) -- the cloud texture (lm_bg04.png) laid 6 times round, scrolling +1 repeat per 60
//    frames and breathing 1 -> 2.07 -> 1 in height, times itself laid 4 times round, scrolling the other way per 80 frames and breathing 1 -> 2.4 -> 1;
//    its alpha the gradient lm_gs001m.png turned 180 (white at the horizon, black at the zenith: the ball fades to the clear colour above ~+25 deg).
//    C0 and C1 are BLACK except the brk's FLASHES over a 300-frame (10 s) cycle: C0 purple at frame 2, green at 154, lavender at 167; C1 grey at
//    12, blue at 118, dark red at 237 -- each a second or two: the "lightning" in the ghost dimension's clouds;
//  * material 'vr_q2', the STREAKS, added on top: a = clamp(2 x sin_wave x sin_wave) x gradient -- the sine-wave texture (lm_sin_wave.png: a bright
//    sine curve with a soft fill under it) laid 8 times round, turned 180, scrolling +1 per 100 frames, its height breathing 3.38 -> 3.84 -> 3.48 ->
//    3.38, times the same laid 10 times round scrolling the other way per 140 frames (3.0 -> 3.38 -> 3.84 -> 3.0), times the gradient mirrored twice
//    (a tent: full 5 deg under the horizon, nothing at the zenith or the dish's rim); colour = mix(magenta (254,60,186), cyan (12,254,242), a) x a.
//    Faint violet bands, cyan where two curves cross.
// The game runs at 30 frames a second; its numbers are display values (the GameCube wrote them to the screen as is), so the result is converted to
// linear at the end. Measured against MarioWiki's Boolossus and Bogmire arena shots the brightness matches (they are darker than they read: the
// band's mean is (30,25,46) with cyan at 253). brightness / streak_gain / base_lift are the knobs for a bolder sky than the game's.
shader_type sky;
render_mode use_debanding;

group_uniforms The_games_textures;
/** The disc's textures (assets/Sms/Rematch/Sky/lm_*.png, 8-bit grey): the clouds, the sine wave, the gradient; 208b: the fire noise and the ramp of the final-boss sky. Fetched by hand (GX wrap per axis). */
uniform sampler2D bg04 : filter_nearest, repeat_enable;
uniform sampler2D sin_wave : filter_nearest, repeat_enable;
uniform sampler2D gs001m : filter_nearest, repeat_enable;
uniform sampler2D gaku_fire01 : filter_nearest, repeat_enable;
uniform sampler2D ramp02 : filter_nearest, repeat_enable;
group_uniforms;

group_uniforms The_final_phases_sky;
/** 208b: how far the sky has gone over to Luigi's Mansion's FINAL-BOSS sky (the King Boo / Bowser roof fight's vrKoopa dome: a dark red backdrop, two layers of
    fire laid round the horizon, red-to-yellow streaks, a red glow below). King Boo drives this at the final split (FinalSky on his node); 1 here = always the final sky. */
uniform float final_blend : hint_range(0.0, 1.0) = 0.0;
/** The final sky's own knobs: the whole of it brighter or dimmer, its fire alone, its streaks alone (1 = the game), a turn of it (degrees). */
uniform float final_brightness : hint_range(0.0, 4.0) = 1.0;
uniform float final_fire_gain : hint_range(0.0, 4.0) = 1.0;
uniform float final_streak_gain : hint_range(0.0, 4.0) = 1.0;
uniform float final_turn : hint_range(-180.0, 180.0) = 0.0;
/** 208c (Jaysonn: "the camera faces down during the fight and the final fire is just red past a certain point -- lower it but keep the fire looking high"): the dome's
    elevations pulled DOWN from its top ring (+22.6 deg stays where it is; everything under it reaches final_stretch times as far down: at 2.5 the fire that ended
    8 deg under the horizon ends 55 deg under it, the red cap under that), the whole dome lowered final_drop degrees on top, and the flame textures laid
    final_flame_tiles times as many times down the taller band (1 = the tongues stretched with it; the band's height / final_flame_tiles keeps their shape).
    1 / 0 / 1 = the game's roof sky exactly. */
uniform float final_stretch : hint_range(1.0, 5.0) = 2.5;
uniform float final_drop : hint_range(-30.0, 60.0) = 0.0;
uniform float final_flame_tiles : hint_range(0.5, 4.0) = 1.6;
group_uniforms;

group_uniforms Knobs;
/** The whole sky brighter or dimmer (1 = the game); the streaks alone (1 = the game); the clouds' flashes (1 = the game's, 0 = never). */
uniform float brightness : hint_range(0.0, 4.0) = 1.0;
uniform float streak_gain : hint_range(0.0, 4.0) = 1.0;
uniform float flash_amount : hint_range(0.0, 2.0) = 1.0;
/** A steady colour on the clouds between the flashes (the game has none: black), and the colour the ball fades to above ~25 deg (the game's clear colour). */
uniform vec3 base_lift : source_color = vec3(0.0, 0.0, 0.0);
uniform vec3 clear_color : source_color = vec3(0.0, 0.0, 0.0);
/** The streaks' two colours: faint = the game's magenta, where two curves cross = the game's cyan. */
uniform vec3 streak_dim : source_color = vec3(0.996, 0.235, 0.729);
uniform vec3 streak_bright : source_color = vec3(0.047, 0.996, 0.949);
/** How fast it all runs (1 = the game's 30 frames a second) and a turn of the whole sky (degrees). */
uniform float speed : hint_range(0.0, 4.0) = 1.0;
uniform float turn : hint_range(-180.0, 180.0) = 0.0;
group_uniforms;

// the dome's v against the elevation (degrees): piecewise linear through its vertex rings
float base_v(float e) {
	if (e >= 55.0) return mix(0.012, 0.0, (e - 55.0) / 35.0);
	if (e >= 37.4) return mix(0.07, 0.012, (e - 37.4) / 17.6);
	if (e >= 22.6) return mix(0.152, 0.07, (e - 22.6) / 14.8);
	if (e >= 7.3) return mix(0.336, 0.152, (e - 7.3) / 15.3);
	if (e >= 0.0) return mix(0.535, 0.336, e / 7.3);
	if (e >= -7.0) return mix(0.734, 0.535, (e + 7.0) / 7.0);
	if (e >= -17.0) return mix(0.902, 0.734, (e + 17.0) / 10.0);
	return mix(1.0, 0.902, (e + 90.0) / 73.0);
}
float dish_v(float e) {
	if (e >= 74.0) return 0.0;
	if (e >= 27.3) return mix(0.035, 0.0, (e - 27.3) / 46.7);
	if (e >= 14.0) return mix(0.078, 0.035, (e - 14.0) / 13.3);
	if (e >= 5.6) return mix(0.148, 0.078, (e - 5.6) / 8.4);
	if (e >= 2.0) return mix(0.227, 0.148, (e - 2.0) / 3.6);
	if (e >= -0.7) return mix(0.312, 0.227, (e + 0.7) / 2.7);
	if (e >= -2.7) return mix(0.406, 0.312, (e + 2.7) / 2.0);
	if (e >= -5.3) return mix(0.52, 0.406, (e + 5.3) / 2.6);
	if (e >= -8.0) return mix(0.629, 0.52, (e + 8.0) / 2.7);
	if (e >= -10.7) return mix(0.727, 0.629, (e + 10.7) / 2.7);
	if (e >= -13.3) return mix(0.809, 0.727, (e + 13.3) / 2.6);
	if (e >= -17.5) return mix(0.902, 0.809, (e + 17.5) / 4.2);
	return mix(1.0, 0.902, (e + 29.0) / 11.5);
}
// a key track: the value eases (cosine) from one key to the next, the way the btk's flat tangents read
float ease(float k) { return 0.5 - 0.5 * cos(3.14159265 * clamp(k, 0.0, 1.0)); }
float key3(float f, float f1, float v0, float v1, float f2, float v2) { return f < f1 ? mix(v0, v1, ease(f / f1)) : mix(v1, v2, ease((f - f1) / (f2 - f1))); }
float key4(float f, float f1, float f2, float v0, float v1, float v2, float f3, float v3) {
	if (f < f1) return mix(v0, v1, ease(f / f1));
	if (f < f2) return mix(v1, v2, ease((f - f1) / (f2 - f1)));
	return mix(v2, v3, ease((f - f2) / (f3 - f2)));
}
// a brk flash: nothing, up over a..b, down over b..c, nothing
float pulse(float f, float a, float b, float c) { return f < a || f > c ? 0.0 : (f < b ? ease((f - a) / (b - a)) : 1.0 - ease((f - b) / (c - b))); }
float mirror1(float x) { return 1.0 - abs(mod(x, 2.0) - 1.0); }
vec3 to_linear(vec3 c) { return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c)); }
// GX texture wrap per axis (0 clamp, 1 repeat, 2 mirror) with bilinear filtering: a sampler's own wrap mode is the same on both axes and a
// mirrored axis fed through a repeating sampler blends the last row into the first at every seam (a dark hairline across the sky), so the four
// texels are fetched by hand.
int wrap_i(int i, int n, int mode) {
	float fi = float(i), fn = float(n);
	if (mode == 0) return int(clamp(fi, 0.0, fn - 1.0));
	if (mode == 1) return int(mod(fi, fn));
	float m = mod(fi, 2.0 * fn); return int(m < fn ? m : 2.0 * fn - 1.0 - m);
}
float tex_wm(sampler2D t, vec2 uv, int mu, int mv) {
	ivec2 sz = textureSize(t, 0);
	vec2 p = uv * vec2(sz) - 0.5; vec2 p0 = floor(p); vec2 f = p - p0;
	int x0 = wrap_i(int(p0.x), sz.x, mu), x1 = wrap_i(int(p0.x) + 1, sz.x, mu);
	int y0 = wrap_i(int(p0.y), sz.y, mv), y1 = wrap_i(int(p0.y) + 1, sz.y, mv);
	return mix(mix(texelFetch(t, ivec2(x0, y0), 0).r, texelFetch(t, ivec2(x1, y0), 0).r, f.x), mix(texelFetch(t, ivec2(x0, y1), 0).r, texelFetch(t, ivec2(x1, y1), 0).r, f.x), f.y);
}

// ---- the boss-arena sky (vrball_B), display-referred
vec3 boss_sky(vec3 d, float e, float f) {
	float u = fract(atan(d.x, d.z) / 6.2831853 + turn / 360.0);
	// ---- the base ball (vr_o2)
	float v0 = base_v(e);
	float fa = mod(f, 60.0), fb = mod(f, 80.0);
	float sa = key3(fa, 30.0, 1.0, 2.0747, 60.0, 1.0), ta = fa / 60.0;
	float sb = key3(fb, 39.0, 1.0, 2.3961, 80.0, 1.0), tb = -fb / 80.0;
	float a = tex_wm(bg04, vec2(6.0 * (u - 0.5) + 0.5 + ta, sa * (v0 - 0.5) + 0.5), 1, 1);
	float b = tex_wm(bg04, vec2(4.0 * (u - 0.5) + 0.5 + tb, sb * (v0 - 0.5) + 0.5), 1, 2);
	float g = tex_wm(gs001m, vec2(1.0 - u, 1.0 - v0), 0, 0);
	float fc = mod(f, 300.0);
	vec3 c0 = (vec3(100.0, 0.0, 150.0) * pulse(fc, 0.0, 2.0, 19.0) + vec3(92.0, 211.0, 97.0) * pulse(fc, 149.0, 154.0, 164.0) + vec3(144.0, 111.0, 168.0) * pulse(fc, 164.0, 167.0, 189.0)) / 255.0 * flash_amount;
	vec3 c1 = (vec3(100.0, 100.0, 100.0) * pulse(fc, 9.0, 12.0, 39.0) + vec3(0.0, 0.0, 73.0) * pulse(fc, 108.0, 118.0, 166.0) + vec3(54.0, 0.0, 0.0) * pulse(fc, 226.0, 237.0, 300.0)) / 255.0 * flash_amount;
	float ab = a * b;
	vec3 base = mix(c1, c0, ab) + base_lift * ab;
	vec3 col = mix(clear_color, base, g);
	// ---- the streaks (vr_q2), added
	if (e > -29.0) {
		float v1 = dish_v(e);
		float f3 = mod(f, 100.0), f4 = mod(f, 140.0);
		float s3 = key4(f3, 35.0, 74.0, 3.38, 3.8428, 3.4801, 100.0, 3.38), t3 = f3 / 100.0;
		float s4 = key4(f4, 53.0, 100.0, 3.0, 3.3834, 3.8428, 140.0, 3.0), t4 = -f4 / 140.0;
		float w1 = tex_wm(sin_wave, vec2(-8.0 * (u - 0.5) + 0.5 + t3, -s3 * (v1 - 0.5) + 0.5 + 1.5), 1, 2);
		float w2 = tex_wm(sin_wave, vec2(-10.0 * (u - 0.5) + 0.5 + t4, -s4 * (v1 - 0.5) + 0.5 + 1.5), 1, 2);
		float g2 = tex_wm(gs001m, vec2(2.0 * (u - 0.5) + 0.5, 2.0 * (v1 - 0.5) + 0.5 - 0.5), 2, 2);
		float amt = clamp(w1 * w2 * 2.0, 0.0, 1.0) * g2 * streak_gain;
		col += mix(streak_dim, streak_bright, amt) * amt;
	}
	return col;
}

// ---- 208b: the final-boss sky (vrKoopa: the King Boo / Bowser roof fight), display-referred. Its dome's v against the elevation, per shape:
float o2_v(float e) {
	if (e >= 22.6) return mix(0.012, 0.0, (e - 22.6) / 67.4);
	if (e >= 7.3) return mix(0.25, 0.012, (e - 7.3) / 15.3);
	if (e >= 0.0) return mix(0.496, 0.25, e / 7.3);
	if (e >= -7.0) return mix(0.727, 0.496, (e + 7.0) / 7.0);
	if (e >= -17.0) return mix(0.902, 0.727, (e + 17.0) / 10.0);
	return mix(1.0, 0.902, (e + 90.0) / 73.0);
}
float p2_v(float e) {
	if (e >= 22.6) return mix(0.027, 0.0, (e - 22.6) / 67.4);
	if (e >= 7.3) return mix(0.266, 0.027, (e - 7.3) / 15.3);
	if (e >= 0.0) return mix(0.504, 0.266, e / 7.3);
	if (e >= -7.0) return mix(0.703, 0.504, (e + 7.0) / 7.0);
	if (e >= -17.0) return mix(0.902, 0.703, (e + 17.0) / 10.0);
	return mix(1.0, 0.902, (e + 90.0) / 73.0);
}
float q2k_v(float e) {
	if (e >= 2.0) return mix(0.18, 0.0, (e - 2.0) / 88.0);
	if (e >= -0.7) return mix(0.285, 0.18, (e + 0.7) / 2.7);
	if (e >= -2.7) return mix(0.391, 0.285, (e + 2.7) / 2.0);
	if (e >= -5.3) return mix(0.523, 0.391, (e + 5.3) / 2.6);
	if (e >= -8.0) return mix(0.668, 0.523, (e + 8.0) / 2.7);
	if (e >= -10.7) return mix(0.789, 0.668, (e + 10.7) / 2.7);
	if (e >= -13.3) return mix(0.887, 0.789, (e + 13.3) / 2.6);
	return mix(1.0, 0.887, (e + 17.5) / 4.2);
}
// one fire ball ('vr_o2' / 'vr_p2'): colour = mix(c0, c1, fire B); alpha = fire A x fire B x the ramp (turned 180: gone below -8 deg) x the clouds (10 x round,
// turned 180, scrolling, v clamped) x the gradient (3 x round, turned 180: gone toward the top ring). Fire A scrolls UP a repeat per 150 frames, fire B per 240.
vec4 fire_layer(float u, float v, float f, vec3 c0, vec3 c1, vec2 fa_s, vec2 fb_s, float bg_sign) {
	float ta = mod(f, 150.0) / 150.0, tb = mod(f, 240.0) / 240.0, tbg = bg_sign * mod(f, 120.0) / 120.0;
	float fa = tex_wm(gaku_fire01, vec2(fa_s.x * (u - 0.5) + 0.5, fa_s.y * (v - 0.5) + 0.5 + 0.82 + ta), 1, 1);
	float fb = tex_wm(gaku_fire01, vec2(fb_s.x * (u - 0.5) + 0.5 + 0.2, fb_s.y * (v - 0.5) + 0.5 + 0.5125 + tb), 1, 1);
	float ramp = tex_wm(ramp02, vec2(1.0 - u, 1.0 - v), 0, 0);
	float bg = tex_wm(bg04, vec2(-10.0 * (u - 0.5) + 0.5 + tbg, -0.834766 * (v - 0.5) + 0.5 - 0.253906), 1, 0);
	float gs = tex_wm(gs001m, vec2(-3.0 * (u - 0.5) + 0.5, 1.0 - v), 1, 0);
	return vec4(mix(c0, c1, fb), fa * fb * ramp * bg * gs);
}
vec3 final_sky(vec3 d, float e_true, float f) {
	float u = fract(atan(d.x, d.z) / 6.2831853 + final_turn / 360.0);
	float e = 22.6 + (e_true + final_drop - 22.6) / max(0.1, final_stretch);           // 208c: the dome pulled down from its top ring
	vec3 col = vec3(33.0, 0.0, 0.0) / 255.0;                                                    // 'vr_outout': the dark red box round everything
	if (e < 22.6) {                                                                              // the fire balls' top ring
		vec4 f1 = fire_layer(u, o2_v(e), f, vec3(234.0, 80.0, 16.0) / 255.0, vec3(254.0, 165.0, 16.0) / 255.0, vec2(6.0, 0.4 * final_flame_tiles), vec2(10.0, 0.5 * final_flame_tiles), 1.0);
		vec4 f2 = fire_layer(fract(u - 75.6 / 360.0), p2_v(e), f, vec3(190.0, 0.0, 0.0) / 255.0, vec3(255.0, 137.0, 0.0) / 255.0, vec2(4.0, 1.5 * final_flame_tiles), vec2(10.0, 1.3 * final_flame_tiles), -1.0);
		col += f1.rgb * f1.a * final_fire_gain + f2.rgb * f2.a * final_fire_gain;
	}
	float cap = clamp((-7.0 - e) / 10.0, 0.0, 1.0);                                             // 'vr_n2': the red glow below, in from -7 deg, full from -17
	col = mix(col, vec3(1.0, 0.0, 0.0), cap);
	if (e <= 2.0 && e >= -17.5) {                                                                // 'vr_q2': the streaks, red to yellow, no 180 turn here
		float vq = q2k_v(e);
		float f5 = mod(f, 100.0), f6 = mod(f, 140.0);
		float s5 = key4(f5, 35.0, 74.0, 1.18, 1.5428, 1.2801, 100.0, 1.18), t5 = f5 / 100.0;
		float s6 = key4(f6, 53.0, 100.0, 1.0, 1.1834, 1.5428, 140.0, 1.0), t6 = -f6 / 140.0;
		float w1 = tex_wm(sin_wave, vec2(8.0 * (u - 0.5) + 0.5 + 0.23 + t5, s5 * (vq - 0.5) + 0.5), 1, 0);
		float w2 = tex_wm(sin_wave, vec2(10.0 * (u - 0.5) + 0.5 - 0.918959 + t6, s6 * (vq - 0.5) + 0.5), 1, 2);
		float g2 = tex_wm(gs001m, vec2(2.0 * (u - 0.5) + 0.5, 2.0 * (vq - 0.5) + 0.5 - 0.5), 2, 2);
		float amt = clamp(w1 * w2 * 2.0, 0.0, 1.0) * g2 * final_streak_gain;
		col += mix(vec3(1.0, 0.0, 0.0), vec3(1.0, 1.0, 0.0), amt) * amt;
	}
	return col;
}

void sky() {
	vec3 d = normalize(EYEDIR);
	float e = degrees(asin(clamp(d.y, -1.0, 1.0)));
	float f = TIME * 30.0 * speed;
	vec3 col = final_blend < 0.999 ? boss_sky(d, e, f) * brightness : vec3(0.0);
	if (final_blend > 0.001) col = mix(col, final_sky(d, e, f) * final_brightness, final_blend);
	COLOR = to_linear(clamp(col, 0.0, 1.0));
}
