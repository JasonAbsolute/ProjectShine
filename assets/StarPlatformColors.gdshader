shader_type spatial;
render_mode blend_mix, cull_disabled, specular_disabled;

uniform sampler2D albedo_tex : source_color;

// 5 point colors matching SMS star platform.
uniform vec3 point_color_0 : source_color = vec3(0.0, 0.50, 0.08);   // Green
uniform vec3 point_color_1 : source_color = vec3(0.75, 0.58, 0.0);   // Yellow/Gold
uniform vec3 point_color_2 : source_color = vec3(0.65, 0.05, 0.55);  // Pink/Magenta
uniform vec3 point_color_3 : source_color = vec3(0.0, 0.48, 0.50);   // Teal
uniform vec3 point_color_4 : source_color = vec3(0.15, 0.45, 0.70);  // Blue

// Rotate which star point gets which color (degrees).
uniform float angle_offset_deg : hint_range(-180.0, 180.0) = 90.0;

// Radius of the star in post-skin units.
// Raw mesh radius ~1.33, skin bind scale ~295 -> ~392.
uniform float star_radius : hint_range(1.0, 600.0) = 392.0;

// Distance from center where color starts (fraction of star_radius).
// 0 = color from center, 1 = only at very tips.
uniform float color_inner : hint_range(0.0, 1.0) = 0.1;

// How strongly the point color replaces the base texture at the tips.
uniform float color_opacity : hint_range(0.0, 1.0) = 0.85;

// Normal threshold for top/bottom vs. side-face detection.
// Faces with abs(normal.y) above this get colored; sides stay texture-only.
uniform float side_mask_threshold : hint_range(0.0, 1.0) = 0.35;

varying vec3 local_pos;
varying vec3 local_nrm;

void vertex() {
	local_pos = VERTEX;
	local_nrm = NORMAL;
}

void fragment() {
	vec4 tex = texture(albedo_tex, UV);
	vec3 N = normalize(local_nrm);

	// Star lies in XZ plane, Y is thin axis.
	float px = local_pos.x;
	float pz = local_pos.z;

	// Angle from center in XZ plane.
	float angle = atan(pz, px);
	float offset_rad = angle_offset_deg * (3.14159265 / 180.0);
	float a = mod(angle - offset_rad + 6.28318530, 6.28318530); // 0..2PI

	// 5 sectors, each 72 degrees (2PI / 5).
	float sector_size = 6.28318530 / 5.0;
	int sector = int(floor(a / sector_size));
	sector = clamp(sector, 0, 4);

	// Pick color for this sector.
	vec3 pt_col;
	if (sector == 0) pt_col = point_color_0;
	else if (sector == 1) pt_col = point_color_1;
	else if (sector == 2) pt_col = point_color_2;
	else if (sector == 3) pt_col = point_color_3;
	else pt_col = point_color_4;

	// Distance from center (normalized to star radius).
	float dist = length(vec2(px, pz)) / star_radius;
	dist = clamp(dist, 0.0, 1.0);

	// Radial gradient: 0 at center, ramps to 1 toward tips.
	float radial = smoothstep(color_inner, 1.0, dist);

	// Side-face mask: only color top/bottom faces, not edges/sides.
	float face_vertical = abs(N.y);
	float top_bottom = smoothstep(side_mask_threshold, side_mask_threshold + 0.15, face_vertical);

	// Final color blend.
	float blend = radial * top_bottom * color_opacity;
	vec3 col = mix(tex.rgb, pt_col, blend);

	ALBEDO = clamp(col, 0.0, 1.0);
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
