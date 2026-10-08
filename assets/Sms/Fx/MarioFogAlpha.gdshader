// Mario's damage fog -- the surfaces whose StandardMaterial3D has transparency ALPHA and back-face culling (_mat_head(4), (6), _mat_eye_L):
// they blend in the transparent pass as before, fogged by their own draw. See MarioFog.gdshaderinc.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;
#define MARIO_FOG_ALPHA
#include "res://assets/Sms/Fx/MarioFog.gdshaderinc"
