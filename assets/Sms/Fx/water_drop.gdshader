// FLUDD's water, the game's draw (TModelWaterManager, ModelWaterManager.cpp): every drop of SmsWaterDrops' MultiMesh is one quad; the
// instance's translation is the drop, INSTANCE_CUSTOM.xyz its velocity x mExtension (world metres a tick), .w its size (metres), or
// -1 for a LANDED drop (then the instance basis is the puddle's plane and extent, set by SmsWaterDrops).
//
// A FLYING drop -- calcDrawVtx (770): the quad is built in VIEW space, flat at the drop's view z, a diamond whose front / back corners
// lie along the on-screen velocity (vtx0 = pos + n + vel x 4.0 (unk5D18), vtx2 = pos - n - vel x 4.0; n = the unit on-screen velocity
// x 1.414 x size / 2) and whose side corners are across it (vtx1 = pos + (n.y, -n.x), vtx3 the other way); UVs (0,0) (1,0) (1,1) (0,1)
// (DLUtil.cpp's uv table), so the texture's diagonal runs along the flight and the disc is drawn out into a streak. A drop with no
// on-screen motion (lenSq <= 1 unit) is an axis-aligned square of half-size 1.414 x size / 2.
// drawRefracAndSpec (1806), three GX passes over that quad, folded into one fragment here:
//   1. the screen behind it (TScreenTexture, the frame at half size 320 x 224) read through an indirect warp: waterref.bti (IA8) by the
//      quad's UV, GX_ITB_STU signed (texel - 128), the 2x3 matrix [0.07 0 0 / 0 0.07 0] (unk5D1C) -> screen TEXELS of offset, S from
//      the texel's alpha, T from its intensity (the indirect stage reads a, b, g); alpha = unk5D65 (255) x waterMask.bti's intensity;
//      blend SRCALPHA / INVSRCALPHA.
//   2. the water colour gModelWaterManagerWaterColor[0] (0x3C, 0x46, 0x78, 0x14), alpha = its 0x14 x the mask; the same blend.
//   3. the highlight: colour = unk5D24 (0x8e, 0x8e, 0x9e) + waterSpec.bti x unk5D20 (0xbc, 0xcc, 0xdc), alpha = waterSpec; blend
//      SRCALPHA / ONE (added).
// All three are display-space (8-bit framebuffer) blends; here the screen is taken to sRGB, the three composed there as the game's
// premultiplied source and its coverage, and the source put back to linear for Godot's own (linear) blend, the coverage its alpha.
// Z test on, no write, no cull, no fog (GX_FOG_NONE), as the game's.
//
// A LANDED drop -- drawWaterVolume (1029): the game draws water_shadow_yuka.bmd (a squashed ball, radius 11.76, placed on the floor at
// the drop, scaled by its size x mMagnify x its life left) into destination alpha, 128 (unk5D5C) where the volume meets the ground,
// then the whole screen's alpha x 80 / 255 (unk5D5D), then the water colour by DSTALPHA / INVDSTALPHA: a flat disc of the water colour
// at 128 x 80 / 255^2 = 0.157 on the ground (two overlapping volumes saturate the alpha: 0.31). Here the same disc is this quad lying in
// the surface's plane, the mask's edge sharpened to the volume's hard edge, the tint at puddle_alpha; no refraction, no highlight.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear, repeat_disable;
uniform sampler2D ref_tex : filter_linear_mipmap, repeat_disable;   // waterref.bti (IA8): alpha -> S warp, intensity -> T warp, 128 = none
uniform sampler2D mask_tex : filter_linear_mipmap, repeat_disable;  // waterMask.bti (I8): the drop's coverage, a soft disc filling the quad
uniform sampler2D spec_tex : filter_linear_mipmap, repeat_disable;  // waterSpec.bti (I8): the highlight, up-left of centre
uniform vec2 screen_texels = vec2(320.0, 224.0);                    // TScreenTexture: SMSGetGameRenderWidth / 2 x Height / 2 (ScreenUtil.cpp 218)
uniform float ind_scale = 0.07;                                     // unk5D1C: screen texels per 8-bit warp step
uniform float refract_alpha = 1.0;                                  // unk5D65 / 255: the refraction pass's alpha before the mask
uniform vec4 water_color = vec4(0.2353, 0.2745, 0.4706, 0.0784);    // gModelWaterManagerWaterColor[0] = (0x3C, 0x46, 0x78, 0x14) / 255, raw 8-bit
uniform vec3 spec_base = vec3(0.5569, 0.5569, 0.6196);              // unk5D24 = (0x8e, 0x8e, 0x9e) / 255
uniform vec3 spec_gain = vec3(0.7373, 0.8000, 0.8627);              // unk5D20 = (0xbc, 0xcc, 0xdc) / 255
uniform float tail = 4.0;                                           // unk5D18: the front / back corners' reach along the velocity (x mExtension already)
uniform float still_len = 0.0134334;                                // one game unit in metres: the game's lenSq > 1 test for a drop with no on-screen motion
uniform float puddle_alpha = 0.157;                                 // unk5D5C / 255 x unk5D5D / 255 = 128 / 255 x 80 / 255
uniform vec2 puddle_edge = vec2(0.5, 0.9);                          // the mask values where a landed disc's edge runs from 0 to full (the volume's edge is hard)

varying float v_puddle;

vec3 to_srgb(vec3 c) {
	c = clamp(c, 0.0, 1.0);
	return mix(12.92 * c, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	float size = INSTANCE_CUSTOM.w;
	if (size < 0.0) {
		// a landed drop: the quad lies where the instance basis puts it (on the floor or the wall, its extent the volume's)
		v_puddle = 1.0;
		POSITION = PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX, 1.0));
	} else {
		v_puddle = 0.0;
		vec3 center_v = (VIEW_MATRIX * MODEL_MATRIX[3]).xyz;
		vec3 vel_v = mat3(VIEW_MATRIX) * INSTANCE_CUSTOM.xyz;    // MTXMultVecSR(viewMtx, vel) x mExtension
		float hs = 1.414 * 0.5 * size;
		vec2 p;
		if (dot(vel_v.xy, vel_v.xy) > still_len * still_len) {
			vec2 n = normalize(vel_v.xy) * hs;
			float along = 1.0 - UV.x - UV.y;                     // +1 the front corner (uv 0,0), -1 the back (uv 1,1), 0 the sides
			float across = UV.x - UV.y;                          // +1 the right corner (uv 1,0), -1 the left (uv 0,1)
			p = center_v.xy + along * (n + vel_v.xy * tail) + across * vec2(n.y, -n.x);
		} else {
			p = center_v.xy + VERTEX.xy * (2.0 * hs);            // the mesh quad: (-0.5, 0.5) at uv (0,0) .. (0.5, -0.5) at uv (1,1)
		}
		POSITION = PROJECTION_MATRIX * vec4(p, center_v.z, 1.0);
	}
}

void fragment() {
	float mask = texture(mask_tex, UV).r;
	if (v_puddle > 0.5) {
		ALBEDO = to_linear(water_color.rgb);
		ALPHA = puddle_alpha * smoothstep(puddle_edge.x, puddle_edge.y, mask);
	} else {
		vec4 w = texture(ref_tex, UV);
		vec2 d = (vec2(w.a, w.r) * 255.0 - 128.0) * ind_scale / screen_texels;
		d.y = -d.y;                                                 // GX T runs down the copy; SCREEN_UV's y runs up
		vec3 scr = to_srgb(texture(screen_tex, clamp(SCREEN_UV + d, vec2(0.0), vec2(1.0))).rgb);
		float spec = texture(spec_tex, UV).r;
		float a_ref = refract_alpha * mask;
		float a_col = water_color.a * mask;
		float a = 1.0 - (1.0 - a_ref) * (1.0 - a_col);              // what the three passes leave of the frame behind: (1 - a_ref)(1 - a_col)
		vec3 src = scr * (a_ref * (1.0 - a_col)) + water_color.rgb * a_col + clamp(spec_base + spec * spec_gain, 0.0, 1.0) * spec;
		ALBEDO = a > 1e-4 ? to_linear(clamp(src / a, 0.0, 1.0)) : vec3(0.0);
		ALPHA = a;
	}
}
