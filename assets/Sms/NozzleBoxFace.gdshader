// The nozzle box's face, as nozzlebox.bmt's TEV draws it: J_nozzleBox is a white panel with the FLUDD logo, the screws and the
// frame in black; the stage puts the nozzle's colour (GX_TEVREG1 -- hover 0000FF, rocket FF0000, turbo 5A5A78) where the texel is
// dark and leaves the panel white: C1 x (1 - tex) + tex. Flat like the game (no specular); both sides drawn (the crash pieces are
// single quads). SmsNozzleBox sets `tint` for its Nozzle.
shader_type spatial;
render_mode cull_disabled, specular_disabled;

uniform vec4 tint : source_color = vec4(0.0, 0.0, 1.0, 1.0);
uniform sampler2D face : source_color, filter_linear_mipmap, repeat_enable;

void fragment() {
	vec3 t = texture(face, UV).rgb;
	ALBEDO = mix(tint.rgb, vec3(1.0), t);
	ROUGHNESS = 1.0;
	METALLIC = 0.0;
}
