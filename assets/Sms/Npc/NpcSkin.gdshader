// SMS NPC skin -- the TEV of mom_model.bmd / mow_model.bmd / kinopio_body.bmd (bmd_mat_dump.py), evaluated in display space the
// way the GX does it (8-bit values, S10 registers above 1), then converted back to linear for Godot. Lit per vertex like the game:
// chan0 = (ambient 128 + the object sun) [x the vertex colour when the material says matSrc VTX], chan1 = the same sun as a
// GX specular light with shininess 50 (GXInitLightShininess), the toon ramp (H_toon_2_4i) looked up by the lit colour (SRTG texgen).
// The pollution mask (H_ma_polmask_sample) / graffiti (H_ma_rak_dummy) stages are evaluated for a CLEAN NPC (mask = 1): the port
// has no NPC pollution yet.
//
// mode 0  Pianta body (_hand_mat, 7 stages): no texture at all -- the skin IS the register colour:
//            body = clamp(0.5 x (C0 + mix(toon, lit, 5/8) - 0.5))   (stage 1 -> REG1; stage 5 adds it back)
//            final = body + spec x (1 - body)                        (stage 6: RASC x (1 - CPREV) + CPREV)
// mode 1  Toad body / mouth (_mat_body, 5 stages): texture, then an additive light term:
//            final = tex + 0.5 x (0.25 + 0.25 toon + 0.75 lit) + 0.5 spec - 0.5
// mode 2  Toad cap / cloth (_mat_cap / _mat_cloth, 6 stages): the texture is a MASK between two registers:
//            base  = mix(C1, C2, tex) (+ spec straight into the base on the cap: spec_gain 1; the cloth adds 0.5 spec at the end)
//            final = base + 0.5 x (0.125 + 0.375 toon + 0.625 lit) - 0.5 + spec x spec_gain
shader_type spatial;
render_mode cull_back, depth_draw_always, unshaded, fog_disabled;

uniform int mode = 0;
uniform sampler2D base_tex : source_color, filter_linear, repeat_enable;   // mode 1: the body texture; mode 2: the mask (N_kinopio_cap / _cloth)
uniform sampler2D toon_tex : filter_linear, repeat_disable;               // H_toon_2_4i: 136 in shadow, 255 lit
uniform vec3 color_a = vec3(1.37, 1.18, 0.0);                              // C0 (mode 0) / C1 (mode 2): GX S10 / 255, may exceed 1
uniform vec3 color_b = vec3(0.9);                                          // C2 (mode 2): the spot / trim colour
uniform float spec_gain = 1.0;                                             // mode 2: 1 on the cap (stage 1 d = RASC), 0.5 on the cloth (stage 5 c = 1/2)
uniform bool use_vertex_color = true;                                      // chan0 matSrc VTX (the Piantas) vs REG (the Toad)
uniform vec3 sun_dir = vec3(0.348, 0.870, 0.348);                          // toward the sun, world space: scene.bin (200000, 500000, 200000)
uniform vec3 sun_color = vec3(1.0);
uniform vec3 ambient = vec3(0.502);                                        // ambColor0 (128, 128, 128)
uniform float shininess = 50.0;
uniform float flash = 0.0;                                                 // an additive white (the pass's "sprayed" / debug flash hook; 0 = the game)

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
	vec3 h = normalize(l + vec3(0.0, 0.0, 1.0));
	float c = ndl >= 0.0 ? max(dot(n, h), 0.0) : 0.0;
	float c2 = c * c;
	float spec = c2 / (0.5 * shininess + (1.0 - 0.5 * shininess) * c2);    // cosatt (0,0,1) / distatt (s/2, 0, 1 - s/2)
	v_spec = clamp(sun_color * spec, 0.0, 1.0);
}

void fragment() {
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;
	vec3 prev;
	if (mode == 0) {
		vec3 body = clamp(0.5 * (color_a + mix(toon, v_lit0, 0.625) - 0.5), 0.0, 1.0);   // stage 1 (bias SUBHALF, scale /2)
		prev = clamp(body + v_spec * (1.0 - body), 0.0, 1.0);                              // stage 6
	} else if (mode == 1) {
		vec3 tex = to_srgb(texture(base_tex, UV).rgb);
		vec3 reg0 = clamp(0.25 + mix(toon, v_lit0, 0.75), 0.0, 1.0);                      // stage 3 (konst 3/4, bias SUBHALF)
		prev = clamp(tex + 0.5 * reg0 + 0.5 * v_spec - 0.5, 0.0, 1.0);                     // stage 4
	} else {
		float m = texture(base_tex, UV).r;                                                 // the I4 mask (0 = color_a, 1 = color_b)
		vec3 base = clamp(mix(color_a, color_b, m) + (spec_gain >= 1.0 ? v_spec : vec3(0.0)), 0.0, 1.0);   // stage 1
		vec3 reg0 = clamp(0.125 + mix(toon, v_lit0, 0.625), 0.0, 1.0);                    // stage 4 (konst 5/8, bias SUBHALF)
		prev = clamp(base + 0.5 * reg0 - 0.5 + (spec_gain >= 1.0 ? vec3(0.0) : spec_gain * v_spec), 0.0, 1.0);   // stage 5
	}
	ALBEDO = to_linear(clamp(prev + flash, 0.0, 1.0));
}
