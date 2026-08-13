shader_type spatial;
render_mode blend_mix, cull_disabled, unshaded, depth_draw_never;
uniform sampler2D albedo_tex : source_color;
uniform vec2 scroll_speed = vec2(0.0, 0.0);
uniform vec4 tint_color : source_color = vec4(1.0);

uniform float alpha_start : hint_range(0.0, 1.0) = 0.001;
uniform float alpha_end   : hint_range(0.0, 1.0) = 0.02;
uniform float edge_power  : hint_range(0.2, 4.0) = 1.0;

uniform float opacity : hint_range(0.0, 1.0) = 0.75;
uniform float min_visible_alpha : hint_range(0.0, 1.0) = 0.30;
uniform float color_boost : hint_range(0.5, 4.0) = 1.6;
uniform float gamma_lift : hint_range(0.5, 2.0) = 0.85;

void fragment() {
    vec2 uv = UV + scroll_speed * TIME;
    vec4 tex = texture(albedo_tex, uv);

    float signal = length(tex.rgb) / 1.7320508;

    float edge_alpha = smoothstep(alpha_start, alpha_end, signal);
    edge_alpha = pow(edge_alpha, edge_power);

    float non_black = step(alpha_start, signal);
    float a = max(edge_alpha, non_black * min_visible_alpha);

    vec3 col = pow(max(tex.rgb, vec3(0.0)), vec3(gamma_lift));
    col *= tint_color.rgb * color_boost;

    ALBEDO = col;
    ALPHA = a * opacity;
}