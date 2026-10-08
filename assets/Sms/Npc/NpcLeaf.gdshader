// SMS Pianta leaves -- the TEV of _leafback_mat in mom_model.bmd / mow_model.bmd (bmd_mat_dump.py, identical on both), evaluated in
// display space the way the GX does it and converted back to linear for Godot (see NpcSkin.gdshader for the lighting: chan0 = ambient
// 128 + the object sun, x the vertex colour (matSrc VTX); COLOR1 = the stage's light 2 through GX_AF_SPEC on matColor1 white, i.e. the
// same specular channel the body's stage 6 and the Toad's cap read -- J3D keeps four channel entries in the order COLOR0, ALPHA0, COLOR1,
// ALPHA1 (J3DColorChan::load's chanTbl), so the dump's `chan2` (lighting on, lightMask 0x04, attn SPEC) is GX_COLOR1; pass 95 read `chan1`,
// which is ALPHA0 and unlit, as the colour channel and rasterised a constant white -- a dirty Pianta's skirt rendered WHITE (pass 97)).
// The game swaps the model's 8 x 8 dummy I_mom_mino_dummyI4 / I_mow_mino_dummyI4 for the stage's /scene/monteMCommon/I_mom_mino_rgba.bti
// (NpcManager.cpp cMonteMRealStrawTexName) -- every stage ships its own; `base_tex` is that texture (SmsNpcProfile.LeafTexture, Delfino
// Plaza's by default).
//
//   stage 0: REG2 = polmask (TEX1) > K0.a (the pollution amount) ? 1 : 0          -- clean = 1, goop = 0 (as every NPC material)
//   stage 1: PREV = rak x (1 - REG2)                                                (TEXC, ZERO, C2, ZERO)
//   stage 2: PREV = PREV + RASC(COLOR1 = spec) x (1 - REG2) + leaf x REG2           (RASC, TEXC, C2, CPREV): clean -> the leaf texture;
//            alpha = leaf.a                                                          goop -> rak + spec: the goop texture with the highlight
//   stage 3: REG1 = C0 + mix(toon, lit, 5/8) - 1/2                                  C0 = (150, 150, 180), bias SUBHALF
//   stage 4: PREV = PREV + 1/2 x C1 - 1/2                                            C1 = REG1 as stage 3 just wrote it (not the register's
//                                                                                   initial (157, 161, 169))
//   alphaCmp GEQUAL 128 (the leaf cut-out), blend NONE + z write (opaque), cull NONE (both faces).
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque, unshaded, fog_disabled;

uniform sampler2D base_tex : source_color, filter_linear, repeat_enable;   // the stage's I_mo?_mino_rgba (RGB5A3 128 x 128)
uniform sampler2D toon_tex : filter_linear, repeat_disable;               // H_toon_2_4i
uniform vec3 color_a = vec3(0.588, 0.588, 0.706);                          // C0 (GX_TEVREG0) / 255 = (150, 150, 180)
uniform bool use_vertex_color = true;                                      // chan0 matSrc VTX
uniform vec3 sun_dir = vec3(0.348, 0.870, 0.348);
uniform vec3 sun_color = vec3(1.0);
uniform vec3 ambient = vec3(0.502);                                        // ambColor0 (128, 128, 128)
uniform float shininess = 50.0;                                            // the stage's spec light: GXInitLightShininess (as NpcSkin)
uniform float alpha_ref = 0.5;                                             // alphaCmp GEQUAL 128
uniform float pollution = 0.0;                                             // GX_KCOLOR0 alpha (NpcBase.cpp 718), 0 = clean
uniform sampler2D pol_mask_tex : filter_linear, repeat_enable;            // H_ma_polmask_sample_i4
uniform sampler2D pol_tex : source_color, filter_linear, repeat_enable;    // the stage's h_ma_rak
uniform bool pol_uv2 = true;                                               // texgen1 / 2 read TEX1

varying vec3 v_lit0;
varying vec3 v_spec;

vec3 to_srgb(vec3 c) {
	c = clamp(c, 0.0, 1.0);
	return mix(12.92 * c, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	vec3 l = normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz);
	float ndl = dot(n, l);
	vec3 lit = clamp(ambient + sun_color * max(ndl, 0.0), 0.0, 1.0);        // GX_DF_CLAMP, GX_AF_SPOT = 1
	v_lit0 = use_vertex_color ? clamp(lit * to_srgb(COLOR.rgb), 0.0, 1.0) : lit;
	// COLOR1: ambColor1 0 + light 2 with GX_AF_SPEC on matColor1 white -- the same channel NpcSkin.gdshader rasterises as `spec`
	vec3 h = normalize(l + vec3(0.0, 0.0, 1.0));
	float c = ndl >= 0.0 ? max(dot(n, h), 0.0) : 0.0;
	float c2 = c * c;
	float spec = c2 / (0.5 * shininess + (1.0 - 0.5 * shininess) * c2);    // cosatt (0,0,1) / distatt (s/2, 0, 1 - s/2)
	v_spec = clamp(sun_color * spec, 0.0, 1.0);
}

void fragment() {
	vec4 texel = texture(base_tex, UV);
	vec3 leaf = to_srgb(texel.rgb);
	vec2 puv = pol_uv2 ? UV2 : UV;
	float mask = floor(texture(pol_mask_tex, puv).r * 255.0 + 0.5) / 255.0;
	float clean = (pollution <= 0.0 || mask > pollution) ? 1.0 : 0.0;
	vec3 rak = to_srgb(texture(pol_tex, puv).rgb);
	vec3 base = clamp(rak * (1.0 - clean) + v_spec * (1.0 - clean) + leaf * clean, 0.0, 1.0);   // stages 1-2: goop -> rak + spec
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;
	vec3 light = clamp(color_a + mix(toon, v_lit0, 0.625) - 0.5, 0.0, 1.0);          // stage 3 -> REG1
	vec3 prev = clamp(base + 0.5 * light - 0.5, 0.0, 1.0);                            // stage 4
	ALBEDO = to_linear(prev);
	ALPHA = texel.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_ref;
}
