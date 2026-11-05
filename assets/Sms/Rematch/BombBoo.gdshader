// SMS pass 158 / 159: the BOMB BOO's body (Galaxy's black Boo, BodyMat_v / BodyM_v). What the files hold (pc/inspect_bombboo.py): BombTeresa.png
// is a dark maroon body (0.42, 0.05, 0.03) with YELLOW eyes (1, 0.78, 0) and a pink mouth; the vertex colours are all dark (mean 0.18 linear, none
// over 0.4); the material's ambient is 64 / 64 / 64 / 150 and its colour registers (0,0,0,-89) / (1023,1023,1023,255) are the ghost recipe's -- the
// stage output doubled. 158 multiplied texture x vertex colour straight (near black) under a strong violet rim at 78 %: "just purple and see thru".
// Now: colour = texture x (vertex colour + `ambient`) x `tint` x `lift` (2 = the recipe's doubling), so the body is a dark red-grey and the eyes
// bright yellow; `opacity` 1 = solid; `face_glow` lets the bright texels (the eyes) emit a little in the dark arena; the rim (`glow_color` x `glow`)
// is faint by default; `flash` whitens the body (the carried Boo's pulse, a timed hold's warning).
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, diffuse_lambert, specular_disabled;

uniform sampler2D base_tex : source_color, filter_linear_mipmap;
uniform vec3 tint : source_color = vec3(1.0);
uniform float lift : hint_range(0.0, 4.0) = 2.0;
uniform float ambient : hint_range(0.0, 1.0) = 0.25;
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform float face_glow : hint_range(0.0, 3.0) = 0.5;
uniform vec3 glow_color : source_color = vec3(0.35, 0.1, 0.55);
uniform float glow : hint_range(0.0, 3.0) = 0.25;
uniform float flash : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec4 t = texture(base_tex, UV);
	vec3 lit = t.rgb * (COLOR.rgb + vec3(ambient)) * tint * lift;
	float lum = dot(t.rgb, vec3(0.299, 0.587, 0.114));
	float face = smoothstep(0.45, 0.7, lum);                      // the yellow eyes (lum 0.76); the pink mouth (0.31) and the body stay dark
	float rim = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 2.5);
	ALBEDO = mix(lit, vec3(1.0), flash);
	EMISSION = t.rgb * face * face_glow + glow_color * glow * rim * (1.0 - flash) + vec3(0.6) * flash;
	ROUGHNESS = 0.8;
	ALPHA = opacity * t.a;
}
