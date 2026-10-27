// The Polluted Piranha's GOOP MOUND (pass 126b/d): gene_pakkun_model1.bmd material _mat2 -- the skirt that sinks into the floor (y -1206 .. 144 u).
// The same three TEV stages as GateKeeperBody.gdshader, plus: blend SRCALPHA / INVSRCALPHA with alpha = RASA x TEXA (the vertex alpha fades the
// mound's rim into the ground, times the goop mark's alpha), and ONE INDIRECT stage: texgen1 (TEX0 through TEXMTX1: scale 2, translation driven by
// gene_pakkun_tex1.btk) reads Q_indtex2_16 (16 x 16) and warps stage 0's goop lookup -- the slow wobble of the goop surface.
// THE DEATH: gene_pakkun_dead1.bpk (bpk_dump.py) animates this material's colour register alpha ONLY -- 255 until frame 250 of the 299-frame dead clip,
// then a Hermite ease down to 0 at frame 300 (tangents -2 a frame): the mound melts away in the last 0.8 s while the stalk has already plunged under
// the floor. mound_alpha carries it (set by SmsGateKeeper from the clip's frame).
//
// THE KNOBS (the inspector's Shader Parameters) -- the same as GateKeeperBody.gdshader's, plus:
//   Goop_colour / goop_warp_scroll    how fast the wobble sheet crawls (the BTK tex1 animation, read as a steady scroll), goop_warp_strength how far it
//                                     bends the goop lookup (in sheet widths).
//   Mound / mound_opacity             a gain over the mound's alpha (1 = the game); mound_alpha is driven by the death (leave it).
shader_type spatial;
render_mode cull_back, unshaded, fog_disabled, blend_mix, depth_draw_opaque;

group_uniforms Goop_colour;
uniform sampler2D goop_texture : filter_linear, repeat_enable;
uniform vec4 goop_colour : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform int goop_colour_mode : hint_enum("Texture (the game)", "Tint", "Recolour") = 0;
uniform float goop_uv_rotation_deg = 6.147;
uniform vec2 goop_uv_offset = vec2(-0.0170867, 0.0);
uniform vec2 goop_uv_scroll = vec2(0.0, -0.04);
uniform sampler2D goop_warp_texture : filter_linear, repeat_enable;
uniform vec2 goop_warp_scroll = vec2(0.0, 0.08);
uniform float goop_warp_strength = 0.03;
group_uniforms Highlight;
uniform sampler2D highlight_texture : filter_linear, repeat_disable;
uniform vec4 highlight_colour : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec2 highlight_scale = vec2(-10.0, -10.0);
uniform float highlight_rotation_deg = 148.002;
uniform vec2 highlight_offset = vec2(0.454102, 1.94336);
uniform vec2 highlight_centre = vec2(0.425, 0.166016);
group_uniforms Damage_fog;
uniform vec4 damage_fog_colour : source_color = vec4(0.7843, 0.0, 0.3137, 1.0);
uniform float damage_fog_amount : hint_range(0.0, 1.0) = 0.0;
group_uniforms Mound;
uniform float mound_opacity : hint_range(0.0, 1.0) = 1.0;
uniform float mound_alpha : hint_range(0.0, 1.0) = 1.0;
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
	vec2 warp = (texture(goop_warp_texture, UV * 2.0 + goop_warp_scroll * TIME).rg - 0.5) * goop_warp_strength;    // the indirect stage
	vec2 uv = rot2(goop_uv_rotation_deg) * (UV - 0.5) + 0.5 + goop_uv_offset + goop_uv_scroll * TIME + warp;
	vec4 goop = texture(goop_texture, uv);
	vec2 hl_uv = highlight_scale * (rot2(highlight_rotation_deg) * (v_nrm - highlight_centre)) + highlight_centre + highlight_offset;
	float hl = texture(highlight_texture, clamp(fract(hl_uv), 0.0, 1.0)).r;
	vec3 prev = clamp(goop_rgb(goop.rgb) + to_display(highlight_colour.rgb) * hl, 0.0, 1.0);
	prev = clamp(prev * COLOR.rgb, 0.0, 1.0);
	prev = mix(prev, to_display(damage_fog_colour.rgb), damage_fog_amount);
	ALBEDO = to_linear(prev);
	ALPHA = clamp(COLOR.a * goop.a * mound_alpha * mound_opacity, 0.0, 1.0);
}
