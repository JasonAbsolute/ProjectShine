// Mario's damage fog -- the surfaces whose StandardMaterial3D has transparency ALPHA and no culling (_mat_head(2), albedo alpha 0: it draws
// nothing, fogged or not). See MarioFog.gdshaderinc.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;
#define MARIO_FOG_ALPHA
#include "res://assets/Sms/Fx/MarioFog.gdshaderinc"
