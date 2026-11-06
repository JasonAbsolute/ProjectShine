// LavaBall.gdshader (SMS 172): the crust of a MOLTEN LAVA BALL -- King Boo's fireballs reworked (Jaysonn: "fireballs should be more like molten
// lava balls"). On a SphereMesh, in its object space (the vertex's direction: no seam): domain-warped 3-D voronoi cells at two scales whose EDGES
// are the cracks, glowing orange with a white-hot middle, some cells wholly molten (pools, pulsing), a dark red heat haze about every crack, a
// dark crust between (two tones of value noise) under a fixed lambert (a light over the eye's right shoulder, view space), a fresnel rim of the
// glow; the whole pattern drifting slowly (flow). heat: 1 = fresh out of the crown, 0 = cooled -- the cracks narrow and dim, the pools fade.
// The maths is tools/projectshine/rematch157/lava_preview.py's, line for line (the CPU preview the look was chosen from).
shader_type spatial;
render_mode cull_back, depth_draw_opaque, unshaded, shadows_disabled, fog_disabled;

uniform vec3 crust_dark : source_color = vec3(0.035, 0.018, 0.012);
uniform vec3 crust_pale : source_color = vec3(0.16, 0.09, 0.06);
uniform vec3 hot_color : source_color = vec3(1.0, 0.85, 0.45);   // the cracks' white-hot middle, the pools' heart
uniform vec3 glow_color : source_color = vec3(1.0, 0.35, 0.04);  // the cracks' orange, the rim
uniform vec3 haze_color : source_color = vec3(0.55, 0.08, 0.0);  // the red heat about the cracks
uniform float heat : hint_range(0.0, 1.0) = 1.0;
uniform float cells = 2.4;                                       // big cells per unit of the sphere's direction
uniform float crack_width = 0.14;
uniform float flow = 0.12;                                       // the pattern's drift (units a second)
uniform float seed = 0.0;
uniform float emissive = 1.0;                                    // x the cracks' brightness

varying vec3 obj_n;

vec3 hash3(vec3 c) {
	return fract(sin(vec3(dot(c, vec3(127.1, 311.7, 74.7)), dot(c, vec3(269.5, 183.3, 246.1)), dot(c, vec3(113.5, 271.9, 124.6)))) * 43758.5453);
}
float vnoise(vec3 p) {
	vec3 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash3(i).x, b = hash3(i + vec3(1.0, 0.0, 0.0)).x, c = hash3(i + vec3(0.0, 1.0, 0.0)).x, d = hash3(i + vec3(1.0, 1.0, 0.0)).x;
	float e = hash3(i + vec3(0.0, 0.0, 1.0)).x, g = hash3(i + vec3(1.0, 0.0, 1.0)).x, h = hash3(i + vec3(0.0, 1.0, 1.0)).x, k = hash3(i + vec3(1.0, 1.0, 1.0)).x;
	return mix(mix(mix(a, b, f.x), mix(c, d, f.x), f.y), mix(mix(e, g, f.x), mix(h, k, f.x), f.y), f.z);
}
// F1, F2 (the two nearest cell points' distances) and the nearest cell's own number (its hash's y)
vec3 voronoi(vec3 p) {
	vec3 i = floor(p), f = fract(p);
	float f1 = 9.0, f2 = 9.0, id = 0.0;
	for (int x = -1; x <= 1; x++) {
		for (int y = -1; y <= 1; y++) {
			for (int z = -1; z <= 1; z++) {
				vec3 g = vec3(float(x), float(y), float(z));
				vec3 o = hash3(i + g);
				float d = length(g + o - f);
				if (d < f1) { f2 = f1; f1 = d; id = o.y; }
				else if (d < f2) { f2 = d; }
			}
		}
	}
	return vec3(f1, f2, id);
}
float sm(float x) { x = clamp(x, 0.0, 1.0); return x * x * (3.0 - 2.0 * x); }

void vertex() {
	obj_n = normalize(VERTEX);
}

void fragment() {
	vec3 n = normalize(obj_n);
	float t = TIME;
	vec3 q = n * cells + vec3(seed) + vec3(0.3, 0.7, 0.2) * (t * flow);
	q += (vnoise(q * 1.7) - 0.5) * 0.6;                                       // the warp: irregular cells
	vec3 vo = voronoi(q);
	float edge = vo.y - vo.x, idh = vo.z;
	float width = crack_width * (0.35 + 0.65 * heat) * (0.5 + vnoise(q * 4.0)); // cracks pinching and widening along their length
	float crack = sm(1.0 - edge / width);
	float core = sm(1.0 - edge / (width * 0.4));
	vec3 vf = voronoi(q * 2.3 + 5.0);                                          // the fine cracks between
	float fedge = vf.y - vf.x;
	float fcrack = sm(1.0 - fedge / (width * 0.45)) * 0.45;
	float pool = step(0.88, idh) * (0.75 + 0.25 * sin(t * 3.0 + idh * 20.0)) * (0.4 + 0.6 * heat);   // some cells wholly molten
	float bright = (0.35 + 0.65 * heat) * emissive;
	vec3 ldir = normalize(vec3(0.35, 0.75, 0.55));                              // view space: over the eye's right shoulder
	float nl = clamp(dot(NORMAL, ldir), 0.0, 1.0);
	vec3 crust = mix(crust_dark, crust_pale, vnoise(q * 3.0));
	vec3 col = crust * (0.35 + 0.65 * nl);
	col += haze_color * exp(-edge * 6.0) * 0.5 * heat;
	float lava = max(max(crack, fcrack), pool * 0.85);
	col = col * (1.0 - lava) + glow_color * lava * bright;
	col += hot_color * max(core, pool * 0.3) * bright;
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	col += glow_color * fres * 0.6 * heat;
	ALBEDO = col;
}
