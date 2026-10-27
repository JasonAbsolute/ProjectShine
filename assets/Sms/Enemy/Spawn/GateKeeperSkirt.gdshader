// The Polluted Piranha's goop mound (pass 126b): gene_pakkun_model1.bmd material _mat2 -- the skirt that sinks into the floor (y -1206 .. 144 u).
// The same three TEV stages as GateKeeperBody.gdshader, plus: blend SRCALPHA / INVSRCALPHA with alpha = RASA x TEXA (the vertex alpha fades the
// mound's rim into the ground; the goop mark's alpha), and ONE INDIRECT stage: texgen1 (TEX0 through TEXMTX1: scale 2, translation driven by
// gene_pakkun_tex1.btk) reads Q_indtex2_16 (16 x 16) and warps stage 0's goop lookup -- the slow wobble of the goop surface.
shader_type spatial;
render_mode cull_back, unshaded, fog_disabled, blend_mix, depth_draw_opaque;

uniform sampler2D goop_tex : filter_linear, repeat_enable;
uniform sampler2D hilight_tex : filter_linear, repeat_disable;
uniform sampler2D ind_tex : filter_linear, repeat_enable;      // Q_indtex2_16: the indirect warp
uniform float goop_rot = 6.147;                                // TEXMTX0: rot 6.14685 deg, trans (-0.0170867, 0)
uniform vec2 goop_trans = vec2(-0.0170867, 0.0);
uniform vec2 goop_scroll = vec2(0.0, -0.04);                   // the BTK tex0 as a steady scroll (DEVIATION: the btk is not read)
uniform vec2 ind_scroll = vec2(0.0, 0.08);                     // the BTK tex1 on TEXMTX1 as a steady scroll
uniform float ind_strength = 0.03;                             // the indirect matrix's gain, in uv
uniform vec2 hl_scale = vec2(-10.0, -10.0);                    // TEXMTX2 (the sphere map) of _mat2
uniform float hl_rot = 148.002;
uniform vec2 hl_trans = vec2(0.454102, 1.94336);
uniform vec2 hl_center = vec2(0.425, 0.166016);
uniform vec3 c1 = vec3(1.0);
uniform vec3 fog_color = vec3(0.7843, 0.0, 0.3137);
uniform float fog_amount = 0.0;
uniform float dead_fade = 0.0;
uniform vec3 dead_color = vec3(0.55, 0.52, 0.5);
uniform float alpha_gain = 1.0;                                // over the material alpha (255): a knob to thin the mound

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
	vec2 warp = (texture(ind_tex, UV * 2.0 + ind_scroll * TIME).rg - 0.5) * ind_strength;    // the indirect stage
	vec2 uv = rot2(goop_rot) * (UV - 0.5) + 0.5 + goop_trans + goop_scroll * TIME + warp;
	vec4 goop = texture(goop_tex, uv);
	vec2 hl_uv = hl_scale * (rot2(hl_rot) * (v_nrm - hl_center)) + hl_center + hl_trans;
	float hl = texture(hilight_tex, clamp(fract(hl_uv), 0.0, 1.0)).r;
	vec3 prev = clamp(goop.rgb + c1 * hl, 0.0, 1.0);
	prev = clamp(prev * COLOR.rgb, 0.0, 1.0);
	prev = mix(prev, dead_color, dead_fade);
	prev = mix(prev, fog_color, fog_amount);
	ALBEDO = to_linear(prev);
	ALPHA = clamp(COLOR.a * goop.a * alpha_gain, 0.0, 1.0);
}
