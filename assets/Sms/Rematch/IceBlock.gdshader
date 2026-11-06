// SMS pass 167: THE ICE about a frozen Mario (the Ghost Breath's freeze; KINGBOO_REMATCH_gdoc.txt: "a hit freezes Mario for one second" -- the port has
// no ice, so this is its own look). A translucent block in an ice blue: the body shows through it tinted; a FRESNEL rim (the edges and the faces turned
// from the camera go brighter and more solid, `rim_color` by `rim_k`, `rim_power` sharpening it); a frost mottle from a soft tile (`frost_tex`, the
// port's stardust, by `frost_k`); a highlight streak sliding slowly down it; `flash` whitens the whole (the pop-in). Unshaded so the arena's violet
// ambient does not turn it lavender (165b). Front faces only.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, shadows_disabled, fog_disabled;

uniform vec4 ice_color : source_color = vec4(0.70, 0.88, 1.0, 0.22);
uniform vec3 rim_color : source_color = vec3(0.92, 0.98, 1.0);
uniform float rim_k : hint_range(0.0, 2.0) = 0.7;
uniform float rim_power : hint_range(0.5, 8.0) = 2.6;
uniform sampler2D frost_tex : filter_linear, repeat_enable;
uniform float frost_k : hint_range(0.0, 1.0) = 0.2;
uniform float flash : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	float facing = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	float rim = pow(1.0 - facing, rim_power);
	float frost = texture(frost_tex, UV * vec2(2.0, 1.5) + vec2(0.0, TIME * 0.02)).r;
	float streak = smoothstep(0.92, 1.0, sin((UV.y * 3.0 - TIME * 0.4) * 6.2831)) * 0.2;
	vec3 c = ice_color.rgb + rim_color * rim * rim_k + vec3(frost * frost_k) + vec3(streak);
	float a = clamp(ice_color.a + rim * 0.45 + frost * frost_k * 0.4 + streak * 0.5, 0.0, 1.0);
	ALBEDO = mix(c, vec3(1.0), flash);
	ALPHA = clamp(a + flash * 0.6, 0.0, 1.0);
}
