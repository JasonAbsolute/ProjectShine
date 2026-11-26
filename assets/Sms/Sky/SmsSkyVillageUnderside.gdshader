shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, skip_vertex_transform, fog_disabled;

// monte_ex0/map/map/sky.bmd. Modes: 0 double-layer star sphere, 1 moon,
// 2 translucent ring (C0 alpha 150/255), 3 cloud band. sky.btk moves the
// cloud texture X from 0 to -1 over 4000 frames at 60 frames/s and loops.
uniform int mode = 0;
uniform sampler2D tex : filter_linear_mipmap, repeat_enable;
uniform float frame_rate = 60.0;
uniform float speed = 1.0;
uniform float loop_frames = 4000.0;
uniform float brightness = 1.0;
varying vec2 v_uv;
varying vec4 v_col;

void vertex() {
    vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz - MODEL_MATRIX[3].xyz + CAMERA_POSITION_WORLD;
    VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
    NORMAL = normalize((VIEW_MATRIX * vec4((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz, 0.0)).xyz);
    v_uv = UV;
    v_col = COLOR;
}

void fragment() {
    float frame = mod(TIME * frame_rate * speed, loop_frames);
    vec4 t;
    if (mode == 0) {
        vec3 a = texture(tex, v_uv * 3.0 + vec2(3.0, -1.2)).rgb;
        vec3 b = texture(tex, v_uv * 2.0).rgb;
        t = vec4(clamp((a * 0.5 + b) * v_col.rgb * 2.0, 0.0, 1.0), 1.0);
    } else if (mode == 3) {
        vec2 uv = (v_uv - vec2(0.5)) * vec2(5.0, 0.7) + vec2(0.5) + vec2(-frame / loop_frames, 0.0);
        t = texture(tex, uv) * v_col;
    } else {
        t = texture(tex, v_uv) * v_col;
        if (mode == 2) t.a *= 150.0 / 255.0;
    }
    ALBEDO = pow(clamp(t.rgb * brightness, 0.0, 1.0), vec3(2.2));
    ALPHA = mode == 0 ? 1.0 : t.a;
}
