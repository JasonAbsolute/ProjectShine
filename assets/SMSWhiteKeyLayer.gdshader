shader_type spatial;
render_mode blend_mix, cull_disabled, unshaded, depth_draw_never;

uniform sampler2D albedo_tex : source_color;
uniform vec2 scroll_speed = vec2(0.0, 0.0);
uniform vec4 tint_color : source_color = vec4(1.0);

uniform float opacity : hint_range(0.0, 1.0) = 0.75;
uniform float color_boost : hint_range(0.5, 4.0) = 1.1;

// Key out near-WHITE instead of near-black
uniform float white_start : hint_range(0.0, 1.0) = 0.90;
uniform float white_end   : hint_range(0.0, 1.0) = 0.99;
uniform float edge_power  : hint_range(0.2, 4.0) = 1.0;

// Keep non-white parts readable
uniform float min_visible_alpha : hint_range(0.0, 1.0) = 0.35;
uniform float gamma_lift : hint_range(0.5, 2.0) = 0.95;

void fragment() {
    vec2 uv = UV + scroll_speed * TIME;
    vec4 tex = texture(albedo_tex, uv);

    // whiteness signal: 1.0 means pure white
    float whiteness = dot(tex.rgb, vec3(0.299, 0.587, 0.114));

    // alpha should be HIGH when pixel is NOT white
    float white_mask = smoothstep(white_start, white_end, whiteness); // 0..1 as it gets white
    float a = 1.0 - white_mask;
    a = pow(max(a, 0.0), edge_power);

    // baseline for visible darker parts
    float not_white = 1.0 - step(white_start, whiteness);
    a = max(a, not_white * min_visible_alpha);

    vec3 col = pow(max(tex.rgb, vec3(0.0)), vec3(gamma_lift));
    col *= tint_color.rgb * color_boost;

    ALBEDO = col;
    ALPHA = a * opacity;
}