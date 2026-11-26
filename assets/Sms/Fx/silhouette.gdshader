// SMS 263: Mario's SILHOUETTE through the map -- TSilhouette (MarioUtil/DrawUtil.cpp): when the camera's view of Mario is blocked
// (MARIO_FLAG_OCCLUDED, the framebuffer peeked at his screen point) the game draws his model again in the silhouette perform list with a flat
// material colour -- gpSunMgr's silhouette colour, (0, 0x27, 0x77) by default -- and the Z test GX_GEQUAL (setting(): "where the map is in front of
// him"), blended SRCALPHA / INVSRCALPHA at an alpha that chases 128 (mOccludedAlpha) at 0.01 of the gap a frame (mAlphaApproachRate) while he is
// hidden and 0 once he is seen again. Here: Mario.SmsSilhouetteTick puts this material on every mesh under his skeleton as a material_overlay while
// the alpha is above zero (the body, the hands, FLUDD) and sets color.a to the alpha / 255; depth_test_inverted draws it only where something
// already drawn is nearer -- the wall in front of him. Where he is in the open only his own far side passes (behind his near side), which tints
// him flat while the alpha fades out, as the game's GEQUAL does. Unlit, no depth write, no shadows, no fog.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, depth_test_inverted, cull_back, shadows_disabled, fog_disabled;

uniform vec4 color : source_color = vec4(0.0, 0.153, 0.467, 0.5);

void fragment() {
	ALBEDO = color.rgb;
	ALPHA = color.a;
}
