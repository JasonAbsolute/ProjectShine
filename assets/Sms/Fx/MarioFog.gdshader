// Mario's damage fog, in the body's own opaque draw -- the opaque, back-culled surfaces (the body: _mat_head*, _mat_eye_R, _mat_mouse).
// See MarioFog.gdshaderinc; SmsBodyOverlay picks the variant from the surface's StandardMaterial3D (transparency, cull mode).
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;
#include "res://assets/Sms/Fx/MarioFog.gdshaderinc"
