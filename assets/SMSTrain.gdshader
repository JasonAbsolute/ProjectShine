shader_type spatial;
render_mode blend_mix, cull_disabled, unshaded, depth_draw_never;

uniform sampler2D albedo_tex : source_color;
uniform vec2 scroll_speed = vec2(0.015, 0.0);

uniform float alpha_start : hint_range(0.0, 1.0) = 0.001;
uniform float alpha_end   : hint_range(0.0, 1.0) = 0.02;
uniform float opacity : hint_range(0.0, 1.0) = 0.75;
uniform float min_visible_alpha : hint_range(0.0, 1.0) = 0.30;
uniform float color_boost : hint_range(0.5, 4.0) = 1.6;
uniform float gamma_lift : hint_range(0.5, 2.0) = 0.85;

// Rainbow hue cycle
uniform float hue_speed : hint_range(0.01, 1.0) = 0.25;

vec3 hsv2rgb(vec3 c) {
    vec3 p = abs(fract(c.xxx + vec3(1.0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0);
    return c.z * mix(vec3(1.0), clamp(p - 1.0, 0.0, 1.0), c.y);
}

void fragment() {
    vec2 uv = UV + scroll_speed * TIME;
    vec4 tex = texture(albedo_tex, uv);

    float signal = length(tex.rgb) / 1.7320508;

    float edge_alpha = smoothstep(alpha_start, alpha_end, signal);
    float non_black = step(alpha_start, signal);
    float a = max(edge_alpha, non_black * min_visible_alpha);

    float brightness = pow(max(signal, 0.0), gamma_lift) * color_boost;

    float hue = fract(TIME * hue_speed);
    vec3 rainbow = hsv2rgb(vec3(hue, 0.7, 0.55));

    vec3 col = rainbow * brightness;

    ALBEDO = col;
    ALPHA = a * opacity;
}
