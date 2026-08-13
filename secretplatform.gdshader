shader_type spatial;

uniform sampler2D albedo_tex : source_color;

void fragment() {
    vec4 tex = texture(albedo_tex, UV);
    ALBEDO = tex.rgb * COLOR.rgb;   // texture * vertex color
}