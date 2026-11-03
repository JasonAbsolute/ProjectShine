// SMS pass 153e: King Boo's CROWN -- BossTelesaToon.gdshader's twin with cull_disabled: btelesa.bmd's MAT3 draws _mat_crown two-sided (cull NONE; the
// body, fangs and mouth cull BACK -- cull153.py), so the inside of the crown shows when you look down into it (Jaysonn: "not rendering the inside of
// the crown"). The material's `part` = 1 picks the crown's stages in the shared include; the inner faces keep the vertex normal, as the game's lighting does.
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_disabled, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/BossTelesa/BossTelesaToon.gdshaderinc"
