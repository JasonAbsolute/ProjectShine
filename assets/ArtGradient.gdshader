shader_type canvas_item;

// Maps flat artwork through a two-colour gradient.
//
// Unlike the Label case, a TextureRect's UV spans the whole image 0–1, so the
// gradient can be driven straight off UV with no helper script.
//
// The texture's own colour is multiplied in rather than replaced, so white art
// takes the gradient outright while shaded art keeps its detail.

uniform vec4 color_a : source_color = vec4(1.0, 0.85, 0.25, 1.0); // centre / top
uniform vec4 color_b : source_color = vec4(0.95, 0.42, 0.04, 1.0); // edge / bottom

// false: vertical, top to bottom.  true: radial, centre outwards.
uniform bool radial = true;

// Pushes the midpoint. <1 keeps more of color_a, >1 more of color_b.
uniform float bias : hint_range(0.2, 4.0) = 1.0;

void fragment() {
    vec4 tex = texture(TEXTURE, UV);

    float t = radial
        ? clamp(length(UV - vec2(0.5)) * 2.0, 0.0, 1.0)
        : UV.y;
    t = pow(t, bias);

    COLOR.rgb = tex.rgb * mix(color_a.rgb, color_b.rgb, t);
    COLOR.a = tex.a * COLOR.a;
}
