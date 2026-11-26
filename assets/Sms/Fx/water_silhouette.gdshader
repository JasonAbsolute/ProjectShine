// SMS 263: FLUDD's water in Mario's SILHOUETTE pass -- TModelWaterManager::drawSilhouette (ModelWaterManager.cpp 952): while Mario is hidden behind
// the map (TSilhouette's alpha above zero, DrawUtil.cpp 100) the game draws his water too where it is hidden: every flying drop as its view-space
// quad (calcDrawVtx's diamond, the same corners as water_drop.gdshader builds) wearing waterJumping.bti (I8 32 x 32, a soft disc), and every landed
// drop's volume (water_shadow_yuka / kabe), all written into the destination alpha and then filled with the silhouette colour (gpSunMgr's, (0, 0x27,
// 0x77) by default) at unk5D5D (80) x the silhouette's alpha / 256 -- so the spray shows through the wall as a faint blue ghost, as Mario does.
// Here: a second MultiMeshInstance3D over SmsWaterDrops' MultiMesh with this material -- the same instances (INSTANCE_CUSTOM.w < 0 = a landed
// drop's disc, else a flying drop's velocity x mExtension and size), drawn ONLY where the depth test fails (depth_test_inverted: behind what is
// already there), the colour the silhouette's, the alpha its fraction x water_alpha x the drop's own shape. Z test inverted, no write, no fog.
shader_type spatial;
render_mode blend_mix, depth_draw_never, depth_test_inverted, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D jump_tex : filter_linear_mipmap, repeat_disable;  // waterJumping.bti (I8): the flying drop's silhouette shape
uniform sampler2D mask_tex : filter_linear_mipmap, repeat_disable;  // waterMask.bti: the landed disc's edge (as water_drop.gdshader)
uniform vec4 color : source_color = vec4(0.0, 0.153, 0.467, 1.0);  // the silhouette colour; .a = the silhouette's alpha fraction (0..1 of its 128)
uniform float water_alpha = 0.3137;                                 // unk5D5D / 255 = 80 / 255
uniform float tail = 4.0;                                           // unk5D18
uniform float still_len = 0.0134334;                                // one game unit in metres
uniform vec2 puddle_edge = vec2(0.5, 0.9);

varying float v_puddle;

void vertex() {
	float size = INSTANCE_CUSTOM.w;
	if (size < 0.0) {
		v_puddle = 1.0;
		POSITION = PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX, 1.0));
	} else {
		v_puddle = 0.0;
		vec3 center_v = (VIEW_MATRIX * MODEL_MATRIX[3]).xyz;
		vec3 vel_v = mat3(VIEW_MATRIX) * INSTANCE_CUSTOM.xyz;
		float hs = 1.414 * 0.5 * size;
		vec2 p;
		if (dot(vel_v.xy, vel_v.xy) > still_len * still_len) {
			vec2 n = normalize(vel_v.xy) * hs;
			float along = 1.0 - UV.x - UV.y;
			float across = UV.x - UV.y;
			p = center_v.xy + along * (n + vel_v.xy * tail) + across * vec2(n.y, -n.x);
		} else {
			p = center_v.xy + VERTEX.xy * (2.0 * hs);
		}
		POSITION = PROJECTION_MATRIX * vec4(p, center_v.z, 1.0);
	}
}

void fragment() {
	float shape = v_puddle > 0.5 ? smoothstep(puddle_edge.x, puddle_edge.y, texture(mask_tex, UV).r) : texture(jump_tex, UV).r;
	ALBEDO = color.rgb;
	ALPHA = color.a * water_alpha * shape;
}
