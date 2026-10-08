// The goop's COVERED INTERIOR, drawn in the OPAQUE pass (pass 96). The body is Goop.gdshaderinc; the soft rim is GoopRim.gdshader, which
// SmsPollutionLayer draws as this material's next_pass with the same uniforms. Opaque because hint_screen_texture (the goop ripple ring and
// balls, the Gooble's body, Mario's damage fog) is a copy of the opaque pass only -- blended, the goop was invisible to those effects.
shader_type spatial;
render_mode depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

#include "res://assets/Sms/Fx/Goop.gdshaderinc"
