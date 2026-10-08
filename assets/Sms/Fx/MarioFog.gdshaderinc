// Mario's body while TMario::addDamageFog is on (MarioDraw.cpp 2185): the surface's own StandardMaterial3D re-stated as a shader so the fog
// is part of the body's OWN draw, the way the GX applies it (a fog on each of the body's materials), instead of a screen-reading overlay
// (pass 56 -> 96). Why it matters (pass 97): Godot's hint_screen_texture is a copy of the OPAQUE pass, and a screen-reading overlay lives
// in the transparent pass -- so the goop ripple ring and balls (goop_refract.gdshader), the Gooble's body and every other refraction saw
// the UNFOGGED Mario and smeared his navy overalls around his fogged legs ("layer issues"); the game's TScreenTexture copy is taken at
// the end of the normal scene stage (PerformLists.bin: 通常シーン描画ステージ DRAW after the player group), fog included. The overlay
// stays for the shock's negative only (BodyOverlay.gdshader), which in the game IS a second pass (drawLogic).
//
// The replica: what Godot's StandardMaterial3D generates for the features Mario's surfaces use (probe97b: albedo colour x texture
// [x vertex colour], roughness, metallic, specular, shading PER_PIXEL, Burley diffuse, Schlick-GGX specular, uv1 scale / offset,
// linear + mipmaps filter, repeat or clamp, transparency DISABLED or ALPHA, cull back or none). SmsBodyOverlay copies each surface's values
// in and leaves a surface alone (with a warning) if it carries a feature this shader has no knob for. The four .gdshader files pick the
// static part: MARIO_FOG_ALPHA (writes ALPHA) and the cull mode.
uniform vec4 albedo : source_color = vec4(1.0);
uniform sampler2D texture_albedo : source_color, hint_default_white, filter_linear_mipmap, repeat_enable;
uniform bool tex_repeat = true;                       // StandardMaterial3D.texture_repeat (off on the body, on on the hands): clamped in the UV
uniform float roughness : hint_range(0.0, 1.0) = 1.0;
uniform float specular : hint_range(0.0, 1.0, 0.01) = 0.5;
uniform float metallic : hint_range(0.0, 1.0, 0.01) = 0.0;
uniform bool use_vertex_color = false;                // vertex_color_use_as_albedo
uniform bool vertex_color_srgb = false;               // vertex_color_is_srgb
uniform vec3 uv1_scale = vec3(1.0);
uniform vec3 uv1_offset = vec3(0.0);
uniform float fog : hint_range(0.0, 1.0) = 0.0;       // SMS_AddDamageFogEffect's linear factor at his depth (7/12 - sin / 4); 0 = off
uniform vec3 fog_color = vec3(1.0, 0.0, 0.5);         // a FRAMEBUFFER (gamma-space) colour: the damage pink (0xFF, 0x00, 0x80) or the goop's (0x82, 0x96, 0x00)

vec3 mf_to_gamma(vec3 c) {
	c = clamp(c, vec3(0.0), vec3(1.0));
	return mix(12.92 * c, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(vec3(0.0031308), c));
}

vec3 mf_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void vertex() {
	UV = UV * uv1_scale.xy + uv1_offset.xy;
	if (use_vertex_color && vertex_color_srgb) {
		COLOR.rgb = mix(pow((COLOR.rgb + vec3(0.055)) * (1.0 / (1.0 + 0.055)), vec3(2.4)), COLOR.rgb * (1.0 / 12.92), lessThan(COLOR.rgb, vec3(0.04045)));
	}
}

void fragment() {
	vec2 uv = tex_repeat ? UV : clamp(UV, vec2(0.0), vec2(1.0));
	vec4 albedo_tex = texture(texture_albedo, uv);
	if (use_vertex_color) {
		albedo_tex *= COLOR;
	}
	ALBEDO = albedo.rgb * albedo_tex.rgb;
	METALLIC = metallic;
	SPECULAR = specular;
	ROUGHNESS = roughness;
#ifdef MARIO_FOG_ALPHA
	ALPHA *= albedo.a * albedo_tex.a;
#endif
	if (fog > 0.0) {
		// The GX fogs the framebuffer's 8-bit (gamma) values: c' = c + (fogColor - c) x f. Godot's FOG mixes the LIT colour in linear light,
		// after this function, and the lit colour is not known here -- so FOG is solved per pixel for the surface lit at 1 (lit = ALBEDO):
		// mix(a, F, fa) = to_linear(mix(to_gamma(a), fog_color, f)). The factor fa is raised above f only as far as keeps F >= 0 (a gamma
		// mix darkens a bright channel more than any non-negative linear fog at f could; a negative F would go negative on a shaded pixel
		// and NaN in the tonemapper). Exact where the body is lit at 1; a shaded pixel lands within a few 8-bit steps of the game's value
		// where a plain linear mix is 30-50 off (fogcheck97.py) -- a documented approximation, pass 97.
		vec3 a = ALBEDO;
		vec3 target = mf_to_linear(mix(mf_to_gamma(a), fog_color, fog));
		vec3 need = vec3(1.0) - target / max(a, vec3(1e-6));
		float fa = clamp(max(fog, max(need.r, max(need.g, need.b))), fog, 1.0);
		vec3 f_col = max((target - (1.0 - fa) * a) / fa, vec3(0.0));
		FOG = vec4(f_col, fa);
	}
}
