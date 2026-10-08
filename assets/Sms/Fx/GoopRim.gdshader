// The goop's SOFT RIM (the edge band under RIM_CUT), blended as the whole goop used to be (pass 96 split; the body is Goop.gdshaderinc).
// SmsPollutionLayer sets it as the goop material's next_pass and copies every uniform across, so the rim always wears the interior's look.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, diffuse_burley, specular_schlick_ggx;

#define GOOP_RIM_PASS
#include "res://assets/Sms/Fx/Goop.gdshaderinc"
