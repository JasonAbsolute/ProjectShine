shader_type canvas_item;

// Reveals a stroke-order map as though it were being painted.
//
// The texture is not artwork in the usual sense: it's baked by
// tools/bake_stroke_order.py from a progressive-draw frame sequence, where
//     R = when that pixel was painted (0 = first stroke, 1 = last)
//     A = the finished shape
// so a single `progress` uniform walks the whole draw. That means one texture
// instead of a frame sequence, a reveal that's smooth at any speed rather than
// locked to the source frame count, and recolouring for free.

uniform vec4 paint_color : source_color = vec4(0.933, 0.016, 0.0, 1.0);
uniform float progress : hint_range(0.0, 1.0) = 0.0;

// Width of the soft leading edge, as a fraction of the whole draw. Reads as a
// wet brush tip; 0 gives a hard cut.
uniform float softness : hint_range(0.0, 0.4) = 0.06;

void fragment() {
    vec4 tex = texture(TEXTURE, UV);

    // Run the window slightly past 1 so the last-painted pixels are fully in by
    // progress = 1. Without this the soft edge leaves them permanently
    // half-drawn, the same way a wipe leaves a lingering line.
    float p = progress * (1.0 + softness);
    float reveal = 1.0 - smoothstep(p - softness, p, tex.r);

    COLOR.rgb = paint_color.rgb;
    COLOR.a *= tex.a * reveal * paint_color.a;
}
