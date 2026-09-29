// J2DPicture's black / white colours: out = black + tex x (white - black), each channel -- the TEV stage every picture pane of the
// console goes through. Identity as saved (black 0000, white FFFF); the life meter turned to air sets black 003CFF00 and white
// 00FFFFFF on its rays and "LIFE" (the lowest ray 0010FFFF), through SmsStandardHud.
shader_type canvas_item;

uniform vec4 black : source_color = vec4(0.0, 0.0, 0.0, 0.0);
uniform vec4 white : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
	vec4 t = texture(TEXTURE, UV);
	COLOR = (black + t * (white - black)) * COLOR;
}
