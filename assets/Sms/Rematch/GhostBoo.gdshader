// SMS pass 164: the WHITE GHOSTS of the rematch -- the BOOLOSSUS (Boolossus.obj: a pale blue-white body swatch (210, 239, 246), yellow eyes, a red
// tongue, pink cheeks, white teeth; no vertex colours, no alpha: "the see-through ghost look has to be authored by hand") and the 15 BOOS it splits
// into (Teresa.dae: the white face sheet teresa.0 x its blue-grey vertex colours, the hotel Boo's TEV recipe doubling the stage). Colour = texture x
// (vertex colour + `ambient`) x `tint` x `lift` when the mesh has vertex colours (`use_vertex_color`), else texture x `tint` x `lift`; `opacity` is the
// ghost's screen mix (King Boo's is 0.72); a soft rim (`glow_color` x `glow`) reads the round body in the dark arena; `flash` whitens it (the Boolossus
// about to land, a Boo about to vanish).
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, diffuse_lambert, specular_disabled;

uniform sampler2D base_tex : source_color, filter_linear_mipmap;
uniform vec3 tint : source_color = vec3(1.0);
uniform float lift : hint_range(0.0, 4.0) = 1.0;
uniform float ambient : hint_range(0.0, 1.0) = 0.35;
uniform bool use_vertex_color = false;
uniform float opacity : hint_range(0.0, 1.0) = 0.72;
uniform vec3 glow_color : source_color = vec3(0.55, 0.7, 1.0);
uniform float glow : hint_range(0.0, 3.0) = 0.5;
uniform float flash : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec4 t = texture(base_tex, UV);
	vec3 shade = use_vertex_color ? (COLOR.rgb + vec3(ambient)) * lift : vec3(lift);
	vec3 lit = min(t.rgb * shade * tint, vec3(1.0));
	float rim = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 2.5);
	ALBEDO = mix(lit, vec3(1.0), flash);
	EMISSION = glow_color * glow * rim * (1.0 - flash) + vec3(0.6) * flash;
	ROUGHNESS = 0.9;
	ALPHA = opacity * t.a;
}
