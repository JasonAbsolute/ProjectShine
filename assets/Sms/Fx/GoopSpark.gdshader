// A spark on electric goop (SmsPollutionLayer's GoopSparks, standing in for MAP_POLLUTION_MS_THUNDER_S): a billboard quad with a
// flickering crackle -- jagged spokes around a bright core, cyan-white, additive. INSTANCE_CUSTOM.x = the spark's age 0..1.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded;

uniform vec4 spark_color : source_color = vec4(0.55, 0.92, 1.0, 1.0);
uniform vec4 core_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float intensity = 1.6;
uniform float spokes = 6.0;
uniform float flicker_rate = 40.0;

varying float age;
varying float inst; // INSTANCE_ID is a vertex built-in: carried over for the per-instance variety

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float vnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i), b = hash21(i + vec2(1.0, 0.0)), c = hash21(i + vec2(0.0, 1.0)), d = hash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void vertex() {
    // a billboard facing the camera, keeping the instance's scale
    float w = length(MODEL_MATRIX[0].xyz);
    float h = length(MODEL_MATRIX[1].xyz);
    mat4 m = mat4(INV_VIEW_MATRIX[0] * w, INV_VIEW_MATRIX[1] * h, INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
    MODELVIEW_MATRIX = VIEW_MATRIX * m;
    MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
    age = INSTANCE_CUSTOM.x;
    inst = float(INSTANCE_ID);
}

void fragment() {
    vec2 p = (UV - vec2(0.5)) * 2.0;
    float r = length(p);
    if (r > 1.0) {
        discard;
    }
    float ang = atan(p.y, p.x);
    float frame = floor(TIME * flicker_rate) + inst * 17.0;
    float jitter = (vnoise(vec2(r * 4.0 + frame, inst)) - 0.5) * 1.2;
    float a = ang / 6.2831853 * spokes + jitter * r;
    float spoke = smoothstep(0.26, 0.0, abs(fract(a) - 0.5)) * smoothstep(1.0, 0.25, r);
    float gate = step(0.35, vnoise(vec2(floor(a) * 3.7 + frame, 2.0)));   // some spokes are missing each flicker frame
    float core = exp(-r * 7.0);
    float fade = 1.0 - smoothstep(0.4, 1.0, age);
    float v = (spoke * gate + core) * fade;
    if (v <= 0.01) {
        discard;
    }
    vec3 col = mix(spark_color.rgb, core_color.rgb, core);
    ALBEDO = col * intensity * v;
    ALPHA = v;
}
