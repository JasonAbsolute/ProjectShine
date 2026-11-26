shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, skip_vertex_transform, fog_disabled;

// sirena_ex0's sky.bmd plus sunset/model.bmd and sunset/glow.bmd. TSky follows the camera.
// TSunModel keeps the authored sun direction but moves it to 250000 game units from the camera.
// BTK and BRK clocks run at 60 animation frames a second. All values below come from the disc files.
uniform int mode = 0;
// 243b: the sheets are sampled RAW (no source_color) -- the maths stays in the game's own colour space and is decoded to linear once at the end
// (the pow 2.2 below), as every other secret sky does; with source_color the sunset's gradients were decoded twice and came out dark and oversaturated.
uniform sampler2D tex0 : filter_linear_mipmap, repeat_enable;
uniform sampler2D tex1 : filter_linear_mipmap, repeat_enable;
uniform vec3 camera_offset = vec3(0.0);
uniform vec2 scale0 = vec2(1.0);
uniform vec2 scale1 = vec2(1.0);
uniform vec2 offset0 = vec2(0.0);
uniform vec2 offset1 = vec2(0.0);
uniform vec3 tint0 = vec3(1.0);
uniform vec3 tint1 = vec3(1.0);
uniform float alpha_scale = 1.0;
uniform float frame_rate = 60.0;
uniform float speed = 1.0;
uniform float brightness = 1.0;

varying vec2 v_uv;
varying vec2 v_uv2;
varying vec4 v_color;

void vertex() {
    vec3 relative = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz - MODEL_MATRIX[3].xyz;
    vec3 world = relative + CAMERA_POSITION_WORLD + camera_offset;
    VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
    NORMAL = normalize((VIEW_MATRIX * vec4((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz, 0.0)).xyz);
    v_uv = UV;
    v_uv2 = UV2;
    v_color = COLOR;
}

vec2 around_center(vec2 uv, vec2 scale_value, vec2 translation) {
    return scale_value * (uv - vec2(0.5)) + vec2(0.5) + translation;
}

vec2 rotate_center(vec2 uv, float angle, vec2 scale_value) {
    float c = cos(angle);
    float s = sin(angle);
    mat2 rotation = mat2(vec2(c, s), vec2(-s, c));
    return rotation * (scale_value * (uv - vec2(0.5))) + vec2(0.5);
}

float glow_k0(float frame) {
    float f = mod(frame, 30.0);
    if (f < 2.0) return mix(10.0, 14.0, smoothstep(0.0, 2.0, f));
    if (f < 5.0) return mix(14.0, 5.0, smoothstep(2.0, 5.0, f));
    if (f < 8.0) return mix(5.0, 12.0, smoothstep(5.0, 8.0, f));
    if (f < 10.0) return mix(12.0, 7.0, smoothstep(8.0, 10.0, f));
    if (f < 13.0) return mix(7.0, 17.0, smoothstep(10.0, 13.0, f));
    if (f < 15.0) return mix(17.0, 8.0, smoothstep(13.0, 15.0, f));
    if (f < 18.0) return mix(8.0, 14.0, smoothstep(15.0, 18.0, f));
    if (f < 20.0) return mix(14.0, 7.0, smoothstep(18.0, 20.0, f));
    if (f < 23.0) return mix(7.0, 13.0, smoothstep(20.0, 23.0, f));
    if (f < 25.0) return mix(13.0, 9.0, smoothstep(23.0, 25.0, f));
    if (f < 27.0) return mix(9.0, 12.0, smoothstep(25.0, 27.0, f));
    return mix(12.0, 9.0, smoothstep(27.0, 30.0, f));
}

void fragment() {
    float frame = TIME * frame_rate * speed;
    vec3 color_value = vec3(0.0);
    float alpha_value = 1.0;
    if (mode == 0) {
        float grade = texture(tex0, v_uv).r;
        color_value = v_color.rgb * (1.0 - grade);
    } else if (mode == 1) {
        float travel = mod(frame, 15000.0) * (5.0 / 15000.0);
        vec4 a = texture(tex0, around_center(v_uv, vec2(2.0), vec2(travel)));
        vec4 b = texture(tex1, around_center(v_uv, vec2(0.625), vec2(0.0)));
        color_value = clamp(a.rgb * b.rgb * tint0 * 2.0, 0.0, 1.0);
        alpha_value = clamp(a.a * b.a * v_color.a * 2.0, 0.0, 1.0);
    } else if (mode == 2) {
        float grade = texture(tex0, v_uv).r;
        color_value = clamp(v_color.rgb + tint0 * (1.0 - grade) - grade * tint1, 0.0, 1.0);
    } else if (mode == 3) {
        float travel = mod(frame, 15000.0) / 15000.0;
        vec4 cloud = texture(tex0, around_center(v_uv, vec2(7.0, 1.0), vec2(travel, 0.0)));
        color_value = cloud.rgb * v_color.rgb;
        alpha_value = cloud.a * alpha_scale;
    } else if (mode == 4) {
        vec3 a = texture(tex0, around_center(v_uv, vec2(2.0), vec2(0.0))).rgb;
        vec3 b = texture(tex1, around_center(v_uv, vec2(3.0), vec2(0.0))).rgb;
        color_value = clamp(v_color.rgb * a * b * 4.0, 0.0, 1.0);
    } else if (mode == 5) {
        vec4 core = texture(tex0, around_center(v_uv, vec2(2.0), vec2(0.0)));
        color_value = mix(vec3(1.0, 0.756863, 0.368627), vec3(1.0, 1.0, 0.941176), core.rgb);
        alpha_value = core.a;
    } else if (mode == 6) {
        vec4 halo = texture(tex0, around_center(v_uv, vec2(1.489258), vec2(0.0)));
        color_value = vec3(0.639216, 0.305882, 0.058824) * halo.rgb;
        alpha_value = halo.a;
    } else if (mode == 7) {
        vec4 radial = texture(tex0, around_center(v_uv, vec2(4.0, 1.0), vec2(0.0)));
        vec3 spectrum = texture(tex1, around_center(v_uv, vec2(1.0, 2.2), vec2(0.0, -0.5))).rgb;
        vec3 c0 = vec3(52.0, 44.0, 0.0) / 255.0;
        vec3 c1 = vec3(44.0) / 255.0;
        color_value = clamp((vec3(glow_k0(frame) / 255.0) + mix(c0, c1, radial.rgb) + spectrum * vec3(1.0, 0.188235, 0.0) + vec3(0.5)) * 0.5, 0.0, 1.0);
        alpha_value = 0.375 * radial.a * 0.25;
    } else {
        float cycle = mod(frame, 4000.0) / 4000.0;
        vec3 a = texture(tex0, rotate_center(v_uv, cycle * TAU, vec2(1.0))).rgb;
        vec3 b = texture(tex1, rotate_center(v_uv, (1.0 - cycle) * TAU, vec2(-1.0, 1.0))).rgb;
        color_value = vec3(0.772549, 0.215686, 0.0) * a * b;
    }
    ALBEDO = pow(clamp(color_value * brightness, 0.0, 1.0), vec3(2.2));
    ALPHA = clamp(alpha_value, 0.0, 1.0);
}
