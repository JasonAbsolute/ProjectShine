// SMS pass 164 / 165: the BOOLOSSUS's body (Boolossus.obj: a pale blue-white body swatch (210, 239, 246), yellow eyes, a red tongue, pink cheeks, white
// teeth; no vertex colours, no alpha: "the see-through ghost look has to be authored by hand"). 165c: UNSHADED, as the port's Boos are (TelesaToon) --
// lit by the scene it read as a lavender blob under the rematch arena's violet ambient from every side but the sun's. A soft shade of its own instead:
// `shade_min`..1 by the normal against a fixed light from the camera's upper left, so the round body still reads. Colour = texture x shade x `tint` x
// `lift` (x (vertex colour + `ambient`) when the mesh has vertex colours, `use_vertex_color`); `opacity` is the ghost's screen mix; a soft rim
// (`glow_color` x `glow`) at the edges; `flash` whitens it (the landing).
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, unshaded;

uniform sampler2D base_tex : source_color, filter_linear_mipmap;
uniform vec3 tint : source_color = vec3(1.0);
uniform float lift : hint_range(0.0, 4.0) = 1.0;
uniform float ambient : hint_range(0.0, 1.0) = 0.35;
uniform bool use_vertex_color = false;
uniform float shade_min : hint_range(0.0, 1.0) = 0.72;
uniform float opacity : hint_range(0.0, 1.0) = 0.9;
uniform vec3 glow_color : source_color = vec3(0.55, 0.7, 1.0);
uniform float glow : hint_range(0.0, 3.0) = 0.5;
uniform float flash : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec4 t = texture(base_tex, UV);
	vec3 n = normalize(NORMAL);                                                  // view space
	float nl = clamp(dot(n, normalize(vec3(-0.35, 0.65, 0.7))), 0.0, 1.0);        // a light from the camera's upper left
	float shade = mix(shade_min, 1.0, nl);
	vec3 base = use_vertex_color ? (COLOR.rgb + vec3(ambient)) * lift : vec3(lift);
	vec3 lit = min(t.rgb * base * tint * shade, vec3(1.0));
	float rim = pow(1.0 - clamp(dot(n, normalize(VIEW)), 0.0, 1.0), 2.5);
	vec3 col = lit + glow_color * glow * rim;
	ALBEDO = mix(col, vec3(1.0), flash);
	ALPHA = opacity * t.a;
}
