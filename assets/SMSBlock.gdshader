shader_type spatial;
render_mode blend_mix, cull_disabled, specular_disabled;

uniform sampler2D albedo_tex : source_color;
uniform vec3 tint_color : source_color = vec3(1.0, 0.88, 0.7);
uniform float saturation_boost : hint_range(0.0, 2.0) = 1.4;
uniform float contrast : hint_range(0.5, 2.0) = 1.3;
uniform float brightness : hint_range(0.5, 2.0) = 1.05;
uniform float vertex_color_strength : hint_range(0.0, 2.0) = 1.5;

void fragment() {
    vec4 tex = texture(albedo_tex, UV);
    vec3 col = tex.rgb;

    // Apply vertex colors (boosted to bring out edge tinting from BMD data)
    col *= mix(vec3(1.0), COLOR.rgb, vertex_color_strength);

    // Apply warm tint to simulate SMS TEV color multiply
    col *= tint_color;

    // Boost saturation (SMS colors are more vivid than PBR default)
    float grey = dot(col, vec3(0.299, 0.587, 0.114));
    col = mix(vec3(grey), col, saturation_boost);

    // Contrast adjustment (centered around 0.5)
    col = (col - 0.5) * contrast + 0.5;

    // Brightness
    col *= brightness;

    ALBEDO = clamp(col, 0.0, 1.0);
    ROUGHNESS = 1.0;
    SPECULAR = 0.0;
}
