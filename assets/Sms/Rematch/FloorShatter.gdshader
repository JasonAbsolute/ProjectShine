// SMS pass 157: the casino table's CRACKS and SHATTER (SmsFloorShatter.cs). The disc's own triangles, re-cut into Voronoi cells about `cells`
// (CUSTOM0 = the cell's pivot xyz + its index): while `crack` grows from 0 to 1 the cell borders show as cracks spreading from `centre` out to
// `radius`; once `shatter_t` runs (seconds since the break) every cell flies as a rigid shard -- kicked outward and up, tumbling about its own
// axis, falling under `gravity` -- and dissolves between fade_start and fade_end. Drawn with the disc's own texture, colour AND material
// (pass 235: metallic / specular / roughness / unshaded / emission copied from the disc's StandardMaterial3D by SmsFloorShatter.MatchMaterial)
// so the whole disc looks unchanged until the first crack -- 157's hard-coded roughness 0.9 on a non-metal turned the metallic table pale and
// glossy the moment it was swapped in, and its glowing crack lines (crack_glow 1.6) read as lit glass. 235: the cracks are dark seams
// (crack_dark) with no glow by default, and the fade is an alpha-scissor dissolve, so the material stays in the OPAQUE pass (the depth prepass,
// the same lighting path as the disc it replaces) instead of 157's blended transparent pass.
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D albedo_tex : source_color, filter_linear;   // the discs' materials sample with TEXTURE_FILTER_LINEAR (no mipmaps): the same here, or the tiled gold goes soft
uniform vec3 uv1_scale = vec3(1.0);         // StandardMaterial3D.uv1_scale / uv1_offset (the gold rim tiles its texture 7 x 7)
uniform vec3 uv1_offset = vec3(0.0);
uniform vec4 albedo_color : source_color = vec4(1.0);
uniform bool has_tex = true;
uniform bool use_vertex_color = false;
uniform bool vertex_color_srgb = false;      // StandardMaterial3D.vertex_color_is_srgb
uniform float roughness : hint_range(0.0, 1.0) = 1.0;    // the disc material's (Godot's defaults when MatchMaterial is off: 1 / 0 / 0.5)
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
uniform float specular : hint_range(0.0, 1.0) = 0.5;
uniform sampler2D rough_tex : hint_default_white, filter_linear_mipmap;   // StandardMaterial3D.roughness_texture x its channel (the glb import's metallic-roughness map)
uniform bool has_rough_tex = false;
uniform vec4 rough_channel = vec4(1.0, 0.0, 0.0, 0.0);
uniform sampler2D metal_tex : hint_default_white, filter_linear_mipmap;   // StandardMaterial3D.metallic_texture x its channel
uniform bool has_metal_tex = false;
uniform vec4 metal_channel = vec4(1.0, 0.0, 0.0, 0.0);
uniform bool unshaded = false;               // StandardMaterial3D shading_mode Unshaded: the colour as is, no light
uniform vec3 emission_color = vec3(0.0);     // the disc material's emission x energy (black = none)
uniform float crack : hint_range(0.0, 1.0) = 0.0;
uniform float crack_width = 0.012;           // the line's width as a fraction of `radius`
uniform float crack_dark = 0.6;              // how dark the seams go (0 = unseen, 1 = to a quarter of the colour)
uniform vec3 crack_color : source_color = vec3(0.85, 0.97, 1.0);
uniform float crack_glow = 0.0;              // emission along the cracks (157's 1.6: lit glass; 235: none)
uniform float shatter_t = 0.0;
uniform float gravity = 14.0;
uniform float spin = 2.5;
uniform float kick = 2.5;
uniform float pop = 1.5;
uniform float rim_delay = 0.18;              // the rim's cells break this much later than the centre's
uniform float fade_start = 1.4;
uniform float fade_end = 2.4;
uniform vec3 centre = vec3(0.0);
uniform float radius = 17.0;
uniform int cell_count = 0;
uniform vec3 cells[128];

varying vec3 v_pos;
varying vec4 v_custom;

float hash1(float n) { return fract(sin(n * 127.1 + 311.7) * 43758.5453); }
float hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

vec3 rot(vec3 p, vec3 axis, float ang) {
	float c = cos(ang), s = sin(ang);
	return p * c + cross(axis, p) * s + axis * dot(axis, p) * (1.0 - c);
}

void vertex() {
	UV = UV * uv1_scale.xy + uv1_offset.xy;
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
	if (use_vertex_color) {
		vec4 vc = COLOR;
		if (vertex_color_srgb) vc.rgb = pow(vc.rgb, vec3(2.2));
		base *= vc;
	}
	vec3 col = base.rgb;
	float line = 0.0;
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
		line = 1.0 - smoothstep(w * 0.35, w, edge);
		// the cracks spread from the centre: a cell's border shows once the reach passes it (a little sooner or later per cell)
		float r = length(v_pos.xz - centre.xz) / max(radius, 0.1);
		float jitter = 0.85 + 0.3 * hash1(float(nearest) + 7.0);
		float shown = 1.0 - smoothstep(reach * 1.08 * jitter - 0.06, reach * 1.08 * jitter, r);
		if (shatter_t > 0.0) shown = 1.0;
		line *= shown;
		col = mix(col, col * 0.25, line * crack_dark);
	}
	vec3 glow = crack_color * crack_glow * line + emission_color;
	float rough = roughness;
	if (has_rough_tex) rough *= dot(texture(rough_tex, UV), rough_channel);
	float metal = metallic;
	if (has_metal_tex) metal *= dot(texture(metal_tex, UV), metal_channel);
	if (unshaded) {
		ALBEDO = vec3(0.0); METALLIC = 0.0; SPECULAR = 0.0; ROUGHNESS = 1.0;
		EMISSION = col + glow;
	} else {
		ALBEDO = col; METALLIC = metal; SPECULAR = specular; ROUGHNESS = rough;
		EMISSION = glow;
	}
	// the shards' fade: a dissolve (alpha scissor against a per-cell speckle: the share of pixels cut grows with the fade), so the material never
	// leaves the opaque pass
	float fade = shatter_t > 0.0 ? smoothstep(fade_start, fade_end, shatter_t) : 0.0;
	float speckle = hash2(floor(v_pos.xz * 24.0) + vec2(v_custom.w * 3.1, 0.0));
	ALPHA = base.a * (1.0 - fade);
	ALPHA_SCISSOR_THRESHOLD = max(0.001, speckle);
}
