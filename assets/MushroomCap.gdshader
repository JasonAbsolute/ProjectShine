shader_type spatial;

// ── Palette uniforms (set per-instance in Mushroom1Up.cs) ──────────────────
uniform vec4 cap_color  : source_color = vec4(0.0,  0.72, 0.0,  1.0);
uniform vec4 spot_color : source_color = vec4(0.82, 1.0,  0.55, 1.0);

uniform float spot_radius   : hint_range(0.2, 1.2)  = 0.52;
uniform float spot_softness : hint_range(0.01, 0.4) = 0.07;

varying vec3 v_normal;

void vertex() {
	v_normal = NORMAL;
}

float dot_spot(vec3 n, vec3 dir, float radius, float soft) {
	float angle = acos(clamp(dot(normalize(n), normalize(dir)), -1.0, 1.0));
	return 1.0 - smoothstep(radius - soft, radius + soft, angle);
}

void fragment() {
	vec3 n = normalize(v_normal);

	// Debug confirmed the transform chain maps axes as follows in shader space:
	//   -Z = UP (world +Y),  +Z = DOWN
	//   -Y = mushroom FRONT (CharacterBody3D local -Z = forward)
	//   +Y = mushroom BACK
	//   ±X = left / right (unchanged)
	// Side spots get a small -Z nudge (0.2) to sit in the upper hemisphere.
	float s = 0.0;
	s = max(s, dot_spot(n, vec3( 0.0,  0.0, -1.0), spot_radius, spot_softness)); // top
	s = max(s, dot_spot(n, vec3( 0.0, -1.0, -0.2), spot_radius, spot_softness)); // front
	s = max(s, dot_spot(n, vec3(-1.0,  0.0, -0.2), spot_radius, spot_softness)); // left
	s = max(s, dot_spot(n, vec3( 1.0,  0.0, -0.2), spot_radius, spot_softness)); // right
	s = max(s, dot_spot(n, vec3( 0.0,  1.0, -0.2), spot_radius, spot_softness)); // back

	ALBEDO = mix(cap_color.rgb, spot_color.rgb, s);
	ROUGHNESS = 0.65;
	METALLIC = 0.0;
}
