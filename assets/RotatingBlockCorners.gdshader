shader_type spatial;
render_mode blend_mix, cull_disabled, specular_disabled;

uniform sampler2D albedo_tex : source_color;

// 8 corner colors matching SMS rotating block (RollingQ)
// Named by axis: P = positive direction, N = negative direction.
uniform vec3 corner_XpYpZp : source_color = vec3(0.0, 0.18, 0.65);   // +X +Y +Z  Blue
uniform vec3 corner_XnYpZp : source_color = vec3(0.68, 0.55, 0.0);   // -X +Y +Z  Yellow
uniform vec3 corner_XpYnZp : source_color = vec3(0.70, 0.45, 0.0);   // +X -Y +Z  Orange
uniform vec3 corner_XnYnZp : source_color = vec3(0.0, 0.42, 0.0);    // -X -Y +Z  Green
uniform vec3 corner_XpYpZn : source_color = vec3(0.65, 0.0, 0.0);    // +X +Y -Z  Red
uniform vec3 corner_XnYpZn : source_color = vec3(0.58, 0.0, 0.58);   // -X +Y -Z  Pink
uniform vec3 corner_XpYnZn : source_color = vec3(0.0, 0.55, 0.55);   // +X -Y -Z  Cyan
uniform vec3 corner_XnYnZn : source_color = vec3(0.60, 0.60, 0.60);  // -X -Y -Z  Gray

// Corner detection: how close ALL 3 axes must be to a wall.
// Higher = tighter corners. Lower = bigger corners. Try 0.4 to 0.7.
uniform float corner_threshold : hint_range(0.1, 0.95) = 0.55;

// Boundary sharpness between corner color and white.
uniform float edge_smoothness : hint_range(0.001, 0.2) = 0.05;

// Half-size of the cube after skeleton skinning.
// Raw mesh is ~1.46, but skin bind pose scales by ~273.759x → ~400.
uniform float box_half_size : hint_range(0.01, 500.0) = 400.0;

varying vec3 local_pos;

void vertex() {
	local_pos = VERTEX;
}

void fragment() {
	vec4 tex = texture(albedo_tex, UV);

	// Normalize vertex position to 0..1 range.
	vec3 t = clamp(local_pos / box_half_size * 0.5 + 0.5, 0.0, 1.0);

	// Which octant / nearest corner.
	float sx = step(0.5, t.x);
	float sy = step(0.5, t.y);
	float sz = step(0.5, t.z);

	// Pick the corner color.
	vec3 corner_color = mix(
		mix(
			mix(corner_XnYnZn, corner_XpYnZn, sx),
			mix(corner_XnYpZn, corner_XpYpZn, sx),
			sy
		),
		mix(
			mix(corner_XnYnZp, corner_XpYnZp, sx),
			mix(corner_XnYpZp, corner_XpYpZp, sx),
			sy
		),
		sz
	);

	// How close each axis is to its nearest wall (1 = at wall, 0 = center).
	vec3 wall_closeness = 1.0 - 2.0 * min(t, 1.0 - t);

	// Corner = ALL THREE axes near a wall. Min is the weakest link.
	// Edge pieces fail because one axis runs through center (min ~ 0).
	float corner_factor = min(min(wall_closeness.x, wall_closeness.y), wall_closeness.z);

	float in_corner = smoothstep(
		corner_threshold - edge_smoothness,
		corner_threshold + edge_smoothness,
		corner_factor
	);

	vec3 col = mix(tex.rgb, corner_color, in_corner);

	ALBEDO = clamp(col, 0.0, 1.0);
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
