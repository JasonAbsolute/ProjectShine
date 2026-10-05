// The Gooble's body, as name.bmd's _mat_body TEV draws it (tools/projectshine/bmd_mat_dump.py on the disc's scene/bianco2/namekuri2/
// name.bmd). Four texture inputs: the SCREEN behind the body (texmap 2 "H_ma_rak_dummy", swapped for TScreenTexture by TNameKuri::init,
// projected by TEXMTX2 = the camera, SMS_GetLightPerspectiveForEffectMtx each frame -> SCREEN_UV), the toon ramp H_toon_2_4i indexed by the
// lit colour (SRTG: 136/255 in shadow, 255 lit, a 153 band at the terminator), K_new_name (8 x 8 IA4: alpha = the V coordinate, a ramp from
// the bottom of the body), and two colour channels: COLOR0 = ambient (128,128,128) + the object sun, clamped; COLOR1 = the sun's specular
// (GX_AF_SPEC / GX_DF_SIGN, ambient 0). The registers: GX_TEVREG0 = the body colour (nameKuriTevColorData, one of 7 per instance),
// GX_TEVREG1 = the charge flash (unk1BC: black, rising to white before the leap). The five stages (out = d + (1 - c) a + c b, bias, scale):
//   0: REG2 = SCREEN x lit0
//   1: PREV = body + mix(toon, lit0, 5/8) - 1/2
//   2: PREV = 2 x (spec + texA x PREV)
//   3: PREV = flash + mix(REG2, PREV, konst0.a = 80/255)      -- 69 % the stage behind it, 31 % the coloured goop
//   4: PREV = PREV + body x texA
// Opaque (blend NONE, z write): the see-through is the screen copy, as in the game. The sun's specular shape (TLightCommon's shininess
// coefficients) is unread: a Blinn-Phong lobe stands in (spec_power). Gooble.cs sets body_color / flash_color / sun_dir / sun_color.
shader_type spatial;
render_mode cull_back, depth_draw_always, unshaded;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear, repeat_disable;
uniform vec3 body_color : source_color = vec3(0.392, 0.0, 0.039);   // GX_TEVREG0 (0x64, 0x00, 0x0A) / 255: the first of the seven
uniform vec3 flash_color : source_color = vec3(0.0);                // GX_TEVREG1
uniform vec3 sun_dir = vec3(0.0, 1.0, 0.0);                          // toward the object sun, world space
uniform vec3 sun_color : source_color = vec3(1.0);
uniform float ambient = 0.502;                                       // ambColor0 128 / 255
uniform float konst_a = 0.3137;                                      // konst0.a 80 / 255 (stage 3's mix)
uniform float toon_shadow = 0.533;                                   // H_toon_2_4i 136 / 255
uniform float spec_power = 24.0;

void fragment() {
	vec3 n = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec3 l = normalize(sun_dir);
	// COLOR0: GX_DF_CLAMP diffuse + the material ambient
	vec3 lit0 = clamp(vec3(ambient) + sun_color * max(dot(n, l), 0.0), 0.0, 1.0);
	// COLOR1: the sun's specular (ambient 0)
	vec3 wpos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 v = normalize(CAMERA_POSITION_WORLD - wpos);
	vec3 h = normalize(l + v);
	vec3 spec = sun_color * pow(max(dot(n, h), 0.0), spec_power);
	// H_toon_2_4i(u = lit0.r, v = lit0.g): 136 below the far corner, 153 on its edge, 255 past it (texels 30 / 31 of 32)
	float s = (lit0.r + lit0.g) * 31.0;
	float toon = s >= 31.5 ? 1.0 : (s >= 29.5 ? 0.6 : toon_shadow);
	// K_new_name: alpha 0 at v 0 to 255 at v 1
	float texA = clamp(UV.y, 0.0, 1.0);
	vec3 screen = texture(screen_tex, SCREEN_UV).rgb;
	vec3 reg2 = screen * lit0;                                                        // stage 0
	vec3 prev = clamp(body_color + mix(vec3(toon), lit0, 0.625) - 0.5, 0.0, 1.0);    // stage 1 (bias SUBHALF)
	prev = clamp(2.0 * (spec + texA * prev), 0.0, 1.0);                              // stage 2 (scale x2)
	prev = clamp(flash_color + mix(reg2, prev, konst_a), 0.0, 1.0);                  // stage 3
	prev = clamp(prev + body_color * texA, 0.0, 1.0);                                // stage 4
	ALBEDO = prev;
}
