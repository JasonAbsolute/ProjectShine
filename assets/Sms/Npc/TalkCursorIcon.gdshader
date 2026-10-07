// TTalkCursor (System/TalkCursor.cpp) -- /common/cursor_b/default.bmd material _mat6, the B-button icon quad (80.27 x 80.27 units, a full
// billboard: shape 0's matrix type 1) 75 units over the cursor point. Two TEV stages (GX, display-referred):
//   stage 0: colour = lerp(C0, C1, TEXC)  with TEXMAP0 = H_action_icon_ia8 (I = the key: 0 -> C0 (240, 68, 68) red, 1 -> C1 (246, 234, 234));
//            alpha  = TEXA x K0.a (255)
//   stage 1: colour = PREV + TEXC x K0, alpha = PREV + TEXA x K0.a  with TEXMAP1 = H_action_icon_b_i4 (the "B", I as colour and alpha)
// K0 is icon_flash.brk's konst track: (200, 44, 44) at frame 0 -> (255, 255, 255) at 49 -> back at 100, one frame a 30 fps game frame
// (TalkCursor.tscn's AnimationPlayer drives k0). perform(CUE_DRAW): GXSetZMode(TRUE, GX_ALWAYS, TRUE) -- the cursor is drawn over the
// world. Blend src alpha / inv src alpha; the material's light channel is unused by its TEV (no RAS input), so unshaded.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_test_disabled, depth_draw_never, shadows_disabled;

uniform sampler2D icon : filter_linear, repeat_disable;      // talk_cursor_icon.png: IA8 as RGB = I, A = A
uniform sampler2D icon_b : filter_linear, repeat_disable;    // talk_cursor_icon_b.png: I4 as RGB = I, A = I
uniform vec4 c0 = vec4(0.941176, 0.266667, 0.266667, 1.0);   // GX_TEVREG0 (240, 68, 68)
uniform vec4 c1 = vec4(0.964706, 0.917647, 0.917647, 1.0);   // GX_TEVREG1 (246, 234, 234)
uniform vec4 k0 = vec4(0.784314, 0.172549, 0.172549, 1.0);   // GX_KCOLOR0, the brk's pulse (frame 0: 200, 44, 44)

vec3 srgb_to_linear(vec3 c) {
    return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
    // the billboard: the view's axes with the model's translation (shape 0 is J3D's billboard matrix type)
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
    MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
    vec4 t0 = texture(icon, UV);
    vec4 t1 = texture(icon_b, UV);
    vec3 col = mix(c0.rgb, c1.rgb, t0.r);          // stage 0 colour: (C0, C1, TEXC, ZERO)
    float a = t0.a * k0.a;                          // stage 0 alpha: (ZERO, TEXA, KONST, ZERO)
    col = clamp(col + t1.r * k0.rgb, 0.0, 1.0);     // stage 1 colour: (ZERO, TEXC, KONST, CPREV)
    a = clamp(a + t1.a * k0.a, 0.0, 1.0);           // stage 1 alpha: (ZERO, TEXA, KONST, APREV)
    ALBEDO = srgb_to_linear(col);
    ALPHA = a;
}
