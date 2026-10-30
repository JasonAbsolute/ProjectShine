// 10_bomb.bmd's one material (_mat2), the Bob-omb blast's fireball (TEffectExplosion, pass 130b), as bmd_mat_dump.py reads it: three texgens off
// TEX0 through three texture matrices (centre 0.5: the magma at scale 1, the magma at scale 2, the sand noise at scale 1; the btk scrolls their
// Ty 0 -> 1, 0 -> 1, 0 -> 0.25 over the 60 frames), three TEV stages:
//   stage0 colour K0 + TEX0 x C0            alpha (1 - K0a) x TEX0A
//   stage1 colour PREV + TEX1 x C1          alpha TEX1A - K0a
//   stage2 colour PREV                      alpha TEX2A - (1 - APREV) x K0a
// opaque (blend NONE, z write), cull NONE, the alpha compare GEQUAL 16 -> the alpha scissor; unlit (no stage reads the rasterised colour). The brk
// drives K0 (rgb the white flash added over everything, 255 -> 0 by frame 42; a the dissolve threshold, 0 -> 255 from frame 12 -- the noise's
// alpha decides where the sphere is eaten) and C0 / C1 (the two magma layers' tints: white -> yellow -> red -> dark). The magma is RGB565 (TEXA 1):
// the chain reduces to alpha = noiseA - K0a^2, so the dissolve follows the sand noise. GX colours are display-referred: the maths is done raw and
// the result put to linear.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque, shadows_disabled, fog_disabled, specular_disabled;

uniform sampler2D magma : filter_linear, repeat_enable;        // P_magmared2 (RGB565 64 x 64, wrap repeat)
uniform sampler2D noise : filter_linear, repeat_enable;        // P_sandnoise2mm (IA8 64 x 64): its alpha is the dissolve noise
uniform vec3 c0 = vec3(1.0, 1.0, 1.0);                         // GX_TEVREG0 (the brk's C0), raw / 255
uniform vec3 c1 = vec3(1.0, 1.0, 1.0);                         // GX_TEVREG1 (C1)
uniform vec4 k0 = vec4(1.0, 1.0, 1.0, 0.0);                    // konst 0: rgb the added flash, a the dissolve threshold
uniform float scroll0 = 0.0;                                   // texmtx0 Ty (btk track 0: frame / 60)
uniform float scroll1 = 0.0;                                   // texmtx1 Ty (track 1, at scale 2)
uniform float scroll2 = 0.0;                                   // texmtx2 Ty (track 2: frame / 240)

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
	vec2 uv0 = UV + vec2(0.0, scroll0);
	vec2 uv1 = (UV - 0.5) * 2.0 + 0.5 + vec2(0.0, scroll1);   // the GX texture matrix scales about its centre (0.5, 0.5), then translates
	vec2 uv2 = UV + vec2(0.0, scroll2);
	vec4 t0 = texture(magma, uv0);
	vec4 t1 = texture(magma, uv1);
	vec4 t2 = texture(noise, uv2);
	vec3 col = clamp(k0.rgb + t0.rgb * c0, 0.0, 1.0);          // stage0
	float a = (1.0 - k0.a) * t0.a;
	col = clamp(col + t1.rgb * c1, 0.0, 1.0);                  // stage1
	a = clamp(t1.a - k0.a, 0.0, 1.0);
	a = clamp(t2.a - (1.0 - a) * k0.a, 0.0, 1.0);              // stage2
	ALBEDO = to_linear(col);
	ALPHA = a;
	ALPHA_SCISSOR_THRESHOLD = 16.0 / 255.0;
}
