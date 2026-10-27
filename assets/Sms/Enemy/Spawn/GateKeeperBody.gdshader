// The Polluted Piranha's STALK AND HEAD (pass 126b/d): /scene/gatekeeper/gene_pakkun_model1.bmd material _mat1 -- opaque, cull BACK, 3 TEV stages:
//   stage 0: PREV = TEXC(goop)                   the stage's goop mark (h_ma_rak.bti swapped into Q_kepper_dummy_128IA4 at init), through TEXMTX0
//                                                (rot 5.26 deg, trans (-0.0146, 0)) + the gene_pakkun_tex0.btk scroll
//   stage 1: PREV = PREV + C1 x TEXC(highlight)   the highlight sheet Q_hilight_keeper4_i4 (32 x 32) looked up by the surface NORMAL (a sphere map, TEXMTX2)
//   stage 2: PREV = PREV x RASC0                  the vertex colour (chan0 matSrc VTX): the baked shading
// The damage fog (SMS_AddDamageFogEffect, DrawUtil.cpp 422): the material's own fog, colour (200, 0, 80), start/end set about the actor's view depth so
// the factor at it pulses 7/12 - sin(moveTick x 0x888) / 4 = 0.33 .. 0.83 with a 30-tick period -- Damage_fog/damage_fog_amount carries it (set by code).
// The death: NOTHING here -- gene_pakkun_dead1.bpk animates only _mat2's alpha (the mound); the stalk plunges below the floor in the dead clip.
// Evaluated in display space like the GX and converted to linear; unshaded (the GX lighting is baked in the vertex colour).
//
// THE KNOBS (the inspector's Shader Parameters):
//   Goop_colour / goop_texture         the goop sheet the body wears -- the game: the STAGE's pollution mark (goop_mark_bianco.png = Bianco's h_ma_rak.bti).
//                                      Any of assets/Sms/Fx/Goop/*.png (Brown, Fire, Electric, Oil, Pink) fits.
//   Goop_colour / goop_colour          a colour for the goop: with goop_colour_mode Tint it multiplies the sheet (white = the sheet as it is), with Recolour
//                                      it REPLACES the sheet's colour and keeps only its light / dark pattern (blue goop, green goop ...). Texture = ignored.
//   Goop_colour / goop_colour_mode     Texture (the game) / Tint / Recolour -- see above.
//   Goop_colour / goop_uv_rotation_deg the sheet's rotation on the body (TEXMTX0: 5.257 deg), goop_uv_offset its shift (-0.0146, 0).
//   Goop_colour / goop_uv_scroll       how fast the sheet crawls over the body, in sheet widths a second (the BTK tex0 animation, read as a steady scroll).
//   Highlight / highlight_texture      the 32 x 32 sheen sheet, looked up by the surface normal.
//   Highlight / highlight_colour       the sheen's gain (GX_TEVREG1 = white); black = no sheen.
//   Highlight / highlight_* (scale, rotation, offset, centre)   TEXMTX2, the sphere-map placement (the game's numbers; leave them).
//   Damage_fog / damage_fog_colour     the fog colour when it is hit ((200, 0, 80) / 255: the game's pink); damage_fog_amount is driven by SmsGateKeeper.
shader_type spatial;
render_mode cull_back, unshaded, fog_disabled;

group_uniforms Goop_colour;
uniform sampler2D goop_texture : filter_linear, repeat_enable;
uniform vec4 goop_colour : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform int goop_colour_mode : hint_enum("Texture (the game)", "Tint", "Recolour") = 0;
uniform float goop_uv_rotation_deg = 5.257;
uniform vec2 goop_uv_offset = vec2(-0.0146167, 0.0);
uniform vec2 goop_uv_scroll = vec2(0.0, -0.04);
group_uniforms Highlight;
uniform sampler2D highlight_texture : filter_linear, repeat_disable;
uniform vec4 highlight_colour : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec2 highlight_scale = vec2(1.12207, 0.908203);
uniform float highlight_rotation_deg = -145.816;
uniform vec2 highlight_offset = vec2(0.371094, -0.0390625);
uniform vec2 highlight_centre = vec2(0.5, 0.537109);
group_uniforms Damage_fog;
uniform vec4 damage_fog_colour : source_color = vec4(0.7843, 0.0, 0.3137, 1.0);
uniform float damage_fog_amount : hint_range(0.0, 1.0) = 0.0;
group_uniforms;

varying vec2 v_nrm;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

vec3 to_display(vec3 c) {
	c = clamp(c, vec3(0.0), vec3(1.0));
	return mix(12.92 * c, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(vec3(0.0031308), c));
}

mat2 rot2(float deg) {
	float a = radians(deg);
	return mat2(vec2(cos(a), sin(a)), vec2(-sin(a), cos(a)));
}

// the goop sheet's colour after the knobs, in display space (source_color uniforms arrive linear)
vec3 goop_rgb(vec3 tex) {
	vec3 c = to_display(goop_colour.rgb);
	if (goop_colour_mode == 1) return tex * c;
	if (goop_colour_mode == 2) return c * (dot(tex, vec3(0.299, 0.587, 0.114)) / 0.5);
	return tex;
}

void vertex() {
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	v_nrm = n.xy * 0.5 + 0.5;
}

void fragment() {
	vec2 uv = rot2(goop_uv_rotation_deg) * (UV - 0.5) + 0.5 + goop_uv_offset + goop_uv_scroll * TIME;
	vec3 goop = goop_rgb(texture(goop_texture, uv).rgb);                                            // stage 0
	vec2 hl_uv = highlight_scale * (rot2(highlight_rotation_deg) * (v_nrm - highlight_centre)) + highlight_centre + highlight_offset;
	float hl = texture(highlight_texture, clamp(hl_uv, 0.0, 1.0)).r;
	vec3 prev = clamp(goop + to_display(highlight_colour.rgb) * hl, 0.0, 1.0);                         // stage 1: PREV + C1 x TEXC
	prev = clamp(prev * COLOR.rgb, 0.0, 1.0);                                                           // stage 2: x RASC0
	prev = mix(prev, to_display(damage_fog_colour.rgb), damage_fog_amount);                             // the damage fog, in display space
	ALBEDO = to_linear(prev);
}
