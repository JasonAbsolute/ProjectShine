// The Gooble's body: name.bmd's _mat_body TEV (tools/projectshine/bmd_mat_dump.py on scene/bianco2/namekuri2/name.bmd), the lights of
// TLightCommon::setLight (LightUtil.cpp) and the Light Group of bianco2's scene.bin, written out stage by stage.
//
// LIGHTS (GX lights the VERTICES; the colours are interpolated -- the lighting lives in vertex(), as the XF unit does):
//   COLOR0 = clamp(ambColor0 (128,128,128) + sun x max(N.L, 0))        GX_LIGHT0 "太陽（オブジェクト）": (255,255,255) from (200000, 500000, 200000), no attenuation
//   COLOR1 = clamp(sun x spec)                                          GX_LIGHT2 = the same sun as a GX_AF_SPEC light, GXInitSpecularDir(-L), shininess 50:
//            spec = c^2 / (s/2 + (1 - s/2) c^2), c = N.H gated by N.L >= 0, H = normalize(L + (0,0,1)) in view space (GXInitLightShininess)
// TEV (out = d + (1 - c) a + c b, then bias and scale; the maths is on the display-referred 8-bit values, so the screen copy is taken to
// sRGB first and the result put back to linear for Godot):
//   0: REG2 = SCREEN x COLOR0                       texmap 2 "H_ma_rak_dummy" = TScreenTexture (TNameKuri::init), texgen POS through TEXMTX2 =
//                                                   the camera's projection (SMS_GetLightPerspectiveForEffectMtx every frame) -> SCREEN_UV
//   1: PREV = REG0 + mix(TOON, COLOR0, 5/8) - 1/2  REG0 = the body colour (nameKuriTevColorData, 7 of them, one per instance in turn);
//                                                   TOON = H_toon_2_4i at (R, G) of COLOR0 (GX_TG_SRTG)
//   2: PREV = 2 x (COLOR1 + K_new_name.A x PREV)    K_new_name (8 x 8 IA4): alpha = a ramp along V -- the skirt and the tufts are coloured, the dome half
//   3: PREV = REG1 + mix(REG2, PREV, 80/255)        REG1 = the charge flash (unk1BC: black, white before the leap); konst0.a = 80
//   4: PREV = PREV + REG0 x K_new_name.A            alpha = RASA0 = 1: opaque, blend NONE, z LEQUAL + write, no fog
// Godot: the screen read puts the material in the transparent pass (the opaque scene is the copy, as the game's capture before the indirect
// scene); depth_draw_always keeps its depth. Gooble.cs duplicates GoobleBody.tres per instance and sets body_color / flash_color, and the
// sun from the scene's DirectionalLight3D (the game's sun is the default here).
shader_type spatial;
render_mode cull_back, depth_draw_always, unshaded, fog_disabled;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear, repeat_disable;
uniform sampler2D body_tex : filter_linear, repeat_disable;     // K_new_name: alpha = the ramp (CLAMP, GX_LINEAR)
uniform sampler2D toon_tex : filter_linear, repeat_disable;     // H_toon_2_4i: 136 in shadow, 255 lit (CLAMP, GX_LINEAR)
uniform vec3 body_color = vec3(0.392, 0.0, 0.039);              // GX_TEVREG0: (0x64, 0x00, 0x0A) / 255, raw 8-bit values
uniform vec3 flash_color = vec3(0.0);                           // GX_TEVREG1
uniform vec3 sun_dir = vec3(0.348, 0.870, 0.348);               // toward the sun, world space: scene.bin (200000, 500000, 200000)
uniform vec3 sun_color = vec3(1.0);                             // (255, 255, 255)
uniform vec3 ambient = vec3(0.502);                             // ambColor0 (128, 128, 128)
uniform float shininess = 50.0;                                 // TLightCommon::mShininess
uniform float konst_a = 0.3137;                                 // konst0.a = 80 / 255

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
	v_lit0 = clamp(ambient + sun_color * max(ndl, 0.0), 0.0, 1.0);          // GX_DF_CLAMP, GX_AF_SPOT with (1,0,0 / 1,0,0) = 1
	vec3 h = normalize(l + vec3(0.0, 0.0, 1.0));
	float c = ndl >= 0.0 ? max(dot(n, h), 0.0) : 0.0;
	float c2 = c * c;
	float spec = c2 / (0.5 * shininess + (1.0 - 0.5 * shininess) * c2);   // cosatt (0,0,1) / distatt (s/2, 0, 1 - s/2)
	v_spec = clamp(sun_color * spec, 0.0, 1.0);
}

void fragment() {
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;
	float ramp = texture(body_tex, UV).a;
	vec3 screen = to_srgb(texture(screen_tex, SCREEN_UV).rgb);
	vec3 reg2 = screen * v_lit0;                                                      // stage 0
	vec3 prev = clamp(body_color + mix(toon, v_lit0, 0.625) - 0.5, 0.0, 1.0);        // stage 1 (bias SUBHALF)
	prev = clamp(2.0 * (v_spec + ramp * prev), 0.0, 1.0);                            // stage 2 (scale x2)
	prev = clamp(flash_color + mix(reg2, prev, konst_a), 0.0, 1.0);                  // stage 3
	prev = clamp(prev + body_color * ramp, 0.0, 1.0);                                // stage 4
	ALBEDO = to_linear(prev);
}
