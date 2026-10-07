// Pass 87: the talk balloon's glyphs and marks drawn from the x4 sheets (gen_talk_hires.py) -- the game draws standard_fontEx.bfn's
// 20 x 20 cells 1:1 on its 640 x 448 frame (J2DTextBox::drawChar_scale, the intensity as the alpha of the box's white); scaled x2.4 to a
// 1080p window that one-texel anti-aliased edge is a 2.4 px blur (Jaysonn: "looks blurry and kinda bad"). The sheet is a Catmull-Rom
// upsample of the disc's coverage, and this sharpens it back to a one-pixel edge: a = smoothstep(0.5 - w, 0.5 + w, c) with w from the
// coverage's screen-space derivative (the alpha-as-distance trick) -- crisp at any Size, and the plain soft bilinear look again once
// the sheet is minified (the HUD's 0.7). `sharpness` 1 = the edge; 0 = the x4 bicubic as it is (still cleaner than the 1x sheet).
shader_type canvas_item;
render_mode blend_mix;

uniform float sharpness : hint_range(0.0, 1.0) = 1.0;
uniform float edge = 0.75;     // the edge's half-width in units of the coverage's per-pixel change (0.75 ~ one pixel of ramp)

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    float c = tex.a;
    float w = max(fwidth(c) * edge, 0.005);
    float crisp = smoothstep(0.5 - w, 0.5 + w, c);
    float a = mix(c, crisp, sharpness);
    COLOR = vec4(COLOR.rgb * tex.rgb, COLOR.a * a);   // the box's colour (setBlackWhite 0xFFFFFF00 / 0xFFFFFFFF, cColorTable) under the sheet's white
}
