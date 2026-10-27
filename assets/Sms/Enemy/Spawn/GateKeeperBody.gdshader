// The Polluted Piranha's body (pass 126b): /scene/gatekeeper/gene_pakkun_model1.bmd material _mat1 (bmd_mat_dump.py) -- opaque, cull BACK,
// 3 TEV stages, 3 texgens:
//   stage 0: PREV = TEXC(texmap0 through TEXMTX0)             the stage's goop mark (h_ma_rak.bti swapped into Q_kepper_dummy_128IA4 at init),
//                                                            TEXMTX0 rot 5.26 deg trans (-0.0146, 0) + the BTK tex0 scroll (gene_pakkun_tex0.btk)
//   stage 1: PREV = PREV + C1 x TEXC(texmap2 through TEXMTX2) the highlight sheet Q_hilight_keeper4_i4 (32 x 32 I4) by the NORMAL (texgen2 src NRM,
//                                                            info 0x06: a sphere map), C1 = (255, 255, 255)
//   stage 2: PREV = PREV x RASC0                              the vertex colour (chan0: lighting off, matSrc VTX) -- the baked shading
//   alpha = RASA (the vertex alpha; unused: blend NONE).
// The damage fog (SMS_AddDamageFogEffect, DrawUtil.cpp 422): the material's own fog, colour (200, 0, 80), start/end set about the actor's view
// depth so the linear factor at it pulses 7/12 - sin(moveTick x 0x888) / 4 = 0.33 .. 0.83 with a period of 30 ticks; fog_amount carries it.
// dead_fade stands in for gene_pakkun_dead1.bpk (a colour animation over the dead clip; no bpk reader yet -- pass 126c).
// Evaluated in display space like the GX and converted to linear; unshaded (the GX lighting is in the vertex colour).
shader_type spatial;
render_mode cull_back, unshaded, fog_disabled;

uniform sampler2D goop_tex : filter_linear, repeat_enable;     // the stage's pollution mark (goop_mark_bianco.png = Bianco's h_ma_rak.bti), read RAW
uniform sampler2D hilight_tex : filter_linear, repeat_disable; // Q_hilight_keeper4_i4
uniform float goop_rot = 5.257;                                // TEXMTX0: rotation (degrees) about (0.5, 0.5)
uniform vec2 goop_trans = vec2(-0.0146167, 0.0);               // TEXMTX0: translation
uniform vec2 goop_scroll = vec2(0.0, -0.04);                   // the BTK tex0 as a steady scroll (uv a second; DEVIATION: the btk is not read)
uniform vec2 hl_scale = vec2(1.12207, 0.908203);               // TEXMTX2 (the sphere map): scale, rotation, translation, centre
uniform float hl_rot = -145.816;
uniform vec2 hl_trans = vec2(0.371094, -0.0390625);
uniform vec2 hl_center = vec2(0.5, 0.537109);
uniform vec3 c1 = vec3(1.0);                                   // GX_TEVREG1 (255, 255, 255): the highlight's gain
uniform vec3 fog_color = vec3(0.7843, 0.0, 0.3137);           // the fog block's colour (200, 0, 80) / 255
uniform float fog_amount = 0.0;                                // the damage fog's factor at the body (0 = off)
uniform float dead_fade = 0.0;                                 // 0 .. 1 over the dead clip toward dead_color
uniform vec3 dead_color = vec3(0.55, 0.52, 0.5);

varying vec2 v_nrm;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

mat2 rot2(float deg) {
	float a = radians(deg);
	return mat2(vec2(cos(a), sin(a)), vec2(-sin(a), cos(a)));
}

void vertex() {
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	v_nrm = n.xy * 0.5 + 0.5;
}

void fragment() {
	vec2 uv = rot2(goop_rot) * (UV - 0.5) + 0.5 + goop_trans + goop_scroll * TIME;
	vec3 goop = texture(goop_tex, uv).rgb;                                       // stage 0
	vec2 hl_uv = hl_scale * (rot2(hl_rot) * (v_nrm - hl_center)) + hl_center + hl_trans;
	float hl = texture(hilight_tex, clamp(hl_uv, 0.0, 1.0)).r;
	vec3 prev = clamp(goop + c1 * hl, 0.0, 1.0);                                 // stage 1: PREV + C1 x TEXC
	prev = clamp(prev * COLOR.rgb, 0.0, 1.0);                                    // stage 2: x RASC0
	prev = mix(prev, dead_color, dead_fade);
	prev = mix(prev, fog_color, fog_amount);                                     // the damage fog, in display space
	ALBEDO = to_linear(prev);
}
