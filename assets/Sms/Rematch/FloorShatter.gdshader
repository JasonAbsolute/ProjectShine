// SMS pass 157: the casino table's CRACKED GLASS and SHATTER (SmsFloorShatter.cs). The disc's own triangles, re-cut into Voronoi cells
// about `cells` (CUSTOM0 = the cell's pivot xyz + its index): while `crack` grows from 0 to 1 the cell borders light up as glass cracks
// spreading from `centre` out to `radius`; once `shatter_t` runs (seconds since the break) every cell flies as a rigid shard -- kicked outward
// and up, tumbling about its own axis, falling under `gravity` -- and fades out between fade_start and fade_end. Drawn with the disc's own
// texture and colour so the whole disc looks unchanged until the first crack.
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_disabled;   // depth written even as a transparent (the fade): the intact disc sorts like the solid one it replaces

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic;
uniform vec4 albedo_color : source_color = vec4(1.0);
uniform bool has_tex = true;
uniform bool use_vertex_color = false;
uniform float roughness : hint_range(0.0, 1.0) = 0.9;
uniform float crack : hint_range(0.0, 1.0) = 0.0;
uniform float crack_width = 0.012;            // the line's width as a fraction of `radius`
uniform vec3 crack_color : source_color = vec3(0.85, 0.97, 1.0);
uniform float crack_glow = 1.6;               // emission strength along the cracks
uniform float shatter_t = 0.0;
uniform float gravity = 14.0;
uniform float spin = 2.5;
uniform float kick = 2.5;
uniform float pop = 1.5;
uniform float rim_delay = 0.18;               // the rim's cells break this much later than the centre's
uniform float fade_start = 1.4;
uniform float fade_end = 2.4;
uniform vec3 centre = vec3(0.0);
uniform float radius = 17.0;
uniform int cell_count = 0;
uniform vec3 cells[128];

varying vec3 v_pos;
varying vec4 v_custom;

float hash1(float n) { return fract(sin(n * 127.1 + 311.7) * 43758.5453); }

vec3 rot(vec3 p, vec3 axis, float ang) {
	float c = cos(ang), s = sin(ang);
	return p * c + cross(axis, p) * s + axis * dot(axis, p) * (1.0 - c);
}

void vertex() {
	v_pos = VERTEX;
	v_custom = CUSTOM0;
	if (shatter_t > 0.0) {
		vec3 c = CUSTOM0.xyz;
		float id = CUSTOM0.w;
		float h1 = hash1(id), h2 = hash1(id + 17.0), h3 = hash1(id + 31.0);
		vec3 out_dir = c - centre;
		out_dir.y = 0.0;
		float l = length(out_dir);
		out_dir = l > 1e-3 ? out_dir / l : vec3(1.0, 0.0, 0.0);
		float t = max(0.0, shatter_t - rim_delay * clamp(l / max(radius, 0.1), 0.0, 1.0));
		vec3 vel = out_dir * kick * (0.3 + 0.9 * h1) + vec3(0.0, pop * (0.2 + h2), 0.0);
		vec3 off = vel * t + vec3(0.0, -0.5 * gravity * t * t, 0.0);
		vec3 axis = normalize(vec3(h1 - 0.5, h2 - 0.5, h3 - 0.5) + vec3(1e-3));
		float ang = spin * (0.4 + h3) * t;
		VERTEX = c + rot(VERTEX - c, axis, ang) + off;
		NORMAL = rot(NORMAL, axis, ang);
	}
}

void fragment() {
	vec4 base = albedo_color;
	if (has_tex) base *= texture(albedo_tex, UV);
	if (use_vertex_color) base *= COLOR;
	ALBEDO = base.rgb;
	ROUGHNESS = roughness;
	float alpha = base.a;
	float reach = crack;
	if (shatter_t > 0.0) reach = 1.0;
	if (reach > 0.0 && cell_count > 1) {
		// the two nearest cells: their border is the crack
		float d1 = 1e9, d2 = 1e9; int nearest = 0;
		for (int i = 0; i < cell_count; i++) {
			float d = distance(v_pos.xz, cells[i].xz);
			if (d < d1) { d2 = d1; d1 = d; nearest = i; }
			else if (d < d2) d2 = d;
		}
		float edge = d2 - d1;
		float w = crack_width * radius;
		float line = 1.0 - smoothstep(w * 0.35, w, edge);
		// the cracks spread from the centre: a cell's border shows once the reach passes it (a little sooner or later per cell)
		float r = length(v_pos.xz - centre.xz) / max(radius, 0.1);
		float jitter = 0.85 + 0.3 * hash1(float(nearest) + 7.0);
		float shown = 1.0 - smoothstep(reach * 1.08 * jitter - 0.06, reach * 1.08 * jitter, r);
		if (shatter_t > 0.0) shown = 1.0;
		line *= shown;
		ALBEDO = mix(ALBEDO, ALBEDO * 0.25, line * 0.6);
		EMISSION = crack_color * crack_glow * line;
	}
	if (shatter_t > 0.0) alpha *= 1.0 - smoothstep(fade_start, fade_end, shatter_t);
	ALPHA = alpha;
}
