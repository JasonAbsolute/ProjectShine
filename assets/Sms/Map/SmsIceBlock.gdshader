shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque, fog_disabled;

// iceblock.bmd material _o1_1: B_IceBlock I8 x raster colour x2.
// Its alpha is I x 4; the texture's minimum I is 80, so every texel clamps opaque.
uniform sampler2D ice_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec3 raster_tint = vec3(1.0, 0.968627, 1.0);

void fragment() {
	vec3 intensity_srgb = pow(texture(ice_texture, UV).rgb, vec3(1.0 / 2.2));
	vec3 result_srgb = clamp(intensity_srgb * raster_tint * 2.0, 0.0, 1.0);
	ALBEDO = pow(result_srgb, vec3(2.2));
}
