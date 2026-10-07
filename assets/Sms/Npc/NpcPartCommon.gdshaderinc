// SMS NPC parts (pass 84) -- the TEV of the hat / glasses / moustache / lapel / tie / luggage / flower / arrow bmds (montemcommon,
// montewcommon, kinopio_sunmegane; bmd_mat_dump.py), evaluated in display space the way the GX does it, converted back to linear for
// Godot. Every part material is two stages over the same per-vertex GX lighting as NpcSkin.gdshader (ambient 128 + the object sun on
// chan0, the sun again as a specular light on chan1, the toon ramp H_toon_2_4i looked up by the lit colour). The forms:
//
//   stage 0 (lit):  s0 = s0_scale * (d + mix(toon, lit, s0_k) + s0_bias)        d: 0 | c0 | a constant (s0_d_mode)
//                   hats A/G/hwA/tie/obi/hige/naka: scale 1/2, bias -1/2, d = c0, k 5/8   (bias SUBHALF, scale /2)
//                   hats E/F, lapel:               scale 1/2, bias -1/2, d = const 3/8 (E, lapel) or 5/8 (F)
//                   hats B/D, glasses, flower:     scale 1,   bias 0,    d = 0, k 5/8
//                   sunglasses:                    scale 1,   bias 0,    d = c0
//                   luggage:                       scale 1,   bias -1/2, d = c0
//                   arrows (s0_form 1):            s0 = lit (one stage, RASC alone)
//   stage 1:        s1_form 0  prev + mix(c1, c2, tex) - 1/2      the two-register mask (hats A/E/F/G, hwA, lapel, tie)
//                   s1_form 1  prev                               the belts (_obi_mat)
//                   s1_form 2  prev * tex                         textured (hats B/D, glasses, sunglasses, flower petals)
//                   s1_form 3  prev + spec * (1 - prev)           the spec channel (moustache, flower centre)
//                   s1_form 4  mix(prev, spec, 1/2) + tex - 1/2   the luggage
//   The register colours come from NpcInitData.cpp's tables through SmsNpcPart (c1 / c2 for kind 2, c0 for kind 1, mat_color for
//   kind 0 = GX_COLOR0, the lit channel's material colour); S10 values above 1 are kept.

uniform int s0_form = 0;             // 0 the lit mix, 1 the raw lit colour (arrows)
uniform float s0_k = 0.625;
uniform int s0_d_mode = 1;           // 0 zero, 1 c0, 2 s0_const
uniform float s0_const = 0.375;
uniform float s0_scale = 0.5;
uniform float s0_bias = -0.5;
uniform int s1_form = 0;
uniform vec3 c0 = vec3(0.784);       // GX_TEVREG0 / 255
uniform vec3 c1 = vec3(0.784);       // GX_TEVREG1 / 255
uniform vec3 c2 = vec3(0.627);       // GX_TEVREG2 / 255
uniform vec3 mat_color = vec3(1.0);  // GX_COLOR0's material colour (chan0 matSrc REG): the lit colour is multiplied by it
uniform bool use_vertex_color = true;   // chan0 matSrc VTX: multiplied by the vertex colour instead
uniform sampler2D base_tex : source_color, filter_linear, repeat_enable;   // texmap0 (the toon is texmap1, sampled by the lit colour)
uniform bool has_tex = true;
uniform sampler2D toon_tex : filter_linear, repeat_disable;
uniform vec3 sun_dir = vec3(0.348, 0.870, 0.348);
uniform vec3 sun_color = vec3(1.0);
uniform vec3 ambient = vec3(0.502);
uniform float shininess = 50.0;

varying vec3 v_lit0;
varying vec3 v_spec;

vec3 to_srgb(vec3 c) {
	c = clamp(c, 0.0, 1.0);
	return mix(12.92 * c, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

// called from vertex() with the view-space normal, the view-space direction toward the sun and the vertex colour (the built-ins are only
// visible inside vertex() itself, and a varying may only be written there): returns the lit colour (chan0) and the specular (chan1)
void part_light(vec3 n, vec3 l, vec3 vcol, out vec3 lit0, out vec3 spec_out) {
	float ndl = dot(n, l);
	vec3 lit = clamp(ambient + sun_color * max(ndl, 0.0), 0.0, 1.0);
	vec3 m = use_vertex_color ? to_srgb(vcol) : mat_color;
	lit0 = clamp(lit * m, 0.0, 1.0);
	vec3 h = normalize(l + vec3(0.0, 0.0, 1.0));
	float c = ndl >= 0.0 ? max(dot(n, h), 0.0) : 0.0;
	float c2s = c * c;
	float spec = c2s / (0.5 * shininess + (1.0 - 0.5 * shininess) * c2s);
	spec_out = clamp(sun_color * spec, 0.0, 1.0);
}

// the display-space colour and the texture alpha (1 without a texture)
vec4 part_color(vec2 uv) {
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;
	vec4 texel = has_tex ? texture(base_tex, uv) : vec4(1.0);
	vec3 tex = to_srgb(texel.rgb);
	vec3 d = s0_d_mode == 1 ? c0 : (s0_d_mode == 2 ? vec3(s0_const) : vec3(0.0));
	vec3 prev = s0_form == 1 ? v_lit0 : clamp(s0_scale * (d + mix(toon, v_lit0, s0_k) + s0_bias), 0.0, 1.0);
	if (s1_form == 0) prev = clamp(prev + mix(c1, c2, tex) - 0.5, 0.0, 1.0);
	else if (s1_form == 2) prev = clamp(prev * tex, 0.0, 1.0);
	else if (s1_form == 3) prev = clamp(prev + v_spec * (1.0 - prev), 0.0, 1.0);
	else if (s1_form == 4) prev = clamp(mix(prev, v_spec, 0.5) + tex - 0.5, 0.0, 1.0);
	return vec4(to_linear(prev), texel.a);
}
