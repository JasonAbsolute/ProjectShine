shader_type canvas_item;

// Two solid panels meeting/parting along one axis, for a fullscreen ColorRect.
// Same progress convention as IrisWipe.gdshader: 0 = fully covered (panels
// meet at the center), 1 = fully open (panels off-screen at the edges).
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform vec4 wipe_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float edge_softness : hint_range(0.0, 0.05) = 0.005;

// false = vertical split (doors travel left/right) — used for the barn-door
// close at the end of the intro pan.
// true = horizontal split (doors travel up/down) — used for the spawn wipe
// that reveals Mario.
uniform bool horizontal = false;

void fragment() {
	float axis_uv = horizontal ? UV.y : UV.x;

	// Width of the still-covered band, centered on 0.5, shrinking to 0 as
	// progress goes 0 -> 1.
	float half_width = (1.0 - progress) * 0.5;
	float d = abs(axis_uv - 0.5);

	float mask = 1.0 - smoothstep(half_width - edge_softness, half_width + edge_softness, d);

	// When the doors are fully open, half_width collapses to 0 — the soft edge
	// then straddles both sides of d=0, but d can never go negative, so the
	// center pixel column/row lands mid-blend (~0.5 alpha) instead of fully
	// transparent. Without this, a faint line lingers at screen center forever
	// after the transition finishes.
	mask *= step(edge_softness, half_width);

	COLOR = vec4(wipe_color.rgb, wipe_color.a * mask);
}
