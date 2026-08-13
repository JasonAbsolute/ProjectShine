shader_type canvas_item;

// Fakes the SMS "candy/beveled" letter look: a vertical brightness gradient
// over the glyph (lighter top, richer bottom). The black outline stays black
// because multiplying near-zero rgb by the gradient is still near-zero — only
// the colored fill picks up the gloss. Preserves the modulate alpha so the
// popup's fade-in/out animation still works.
uniform float top_bright : hint_range(1.0, 2.0) = 1.45;
uniform float bottom_bright : hint_range(0.4, 1.0) = 0.72;
uniform float highlight_band : hint_range(0.0, 0.5) = 0.18;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float g = mix(top_bright, bottom_bright, clamp(UV.y, 0.0, 1.0));
	// A brighter specular band near the top third for the glossy highlight.
	float band = smoothstep(highlight_band + 0.12, highlight_band, UV.y);
	vec3 lit = tex.rgb * g + tex.rgb * band * 0.5;
	COLOR = vec4(lit * COLOR.rgb, tex.a * COLOR.a);
}
