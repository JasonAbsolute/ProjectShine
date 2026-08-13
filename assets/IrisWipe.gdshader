shader_type canvas_item;

// Circular reveal/cover mask for a fullscreen ColorRect. Same progress
// convention as BarnDoorWipe.gdshader: 0 = fully covered, 1 = fully open —
// play the AnimationPlayer track forward for an "in" (reveal) and backward
// for an "out" (cover).
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform vec4 wipe_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float edge_softness : hint_range(0.0, 0.1) = 0.01;

// Actual pixel size of the rect this shader is applied to — set from code
// (rect_size changes with viewport/window size, no built-in gives it to a
// canvas_item fragment shader directly) so the circle stays round instead of
// stretching into an ellipse on non-square viewports.
uniform vec2 rect_size = vec2(1920.0, 1080.0);

void fragment() {
	vec2 centered = (UV - vec2(0.5)) * rect_size;
	float dist = length(centered);

	// Reaches exactly the corners at progress = 1.
	float max_radius = length(rect_size) * 0.5;
	float radius = max_radius * progress;
	float edge = max_radius * edge_softness;

	float mask = smoothstep(radius - edge, radius + edge, dist);
	COLOR = vec4(wipe_color.rgb, wipe_color.a * mask);
}
