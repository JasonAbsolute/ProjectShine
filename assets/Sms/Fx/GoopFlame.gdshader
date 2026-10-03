// A flame on fire goop (SmsPollutionLayer's GoopFlames, standing in for MAP_POLLUTION_MS_NEWFIRE_B / _A). From the reference (Pianta Village, The
// Goopy Inferno): tall, thin, pale yellow-white wisps -- translucent columns of fire standing on the goop, wavering a little, their tops
// dissolving. The game's glow3_flame sprite (a bright vertical slit in a soft glow) on an upright billboard stretched tall (the instance's x
// scale = width, y scale = height), additive, its texture rippling upward, sampled twice out of phase so it flickers. INSTANCE_CUSTOM.x = the
// flame's age 0..1 (it grows up out of the goop, burns, fades), .y = a per-flame random 0..1.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded;

uniform sampler2D flame_tex : source_color, filter_linear, repeat_disable;
uniform vec4 flame_color : source_color = vec4(1.0, 0.93, 0.62, 1.0);   // the wisp: pale yellow
uniform vec4 core_color : source_color = vec4(1.0, 1.0, 0.9, 1.0);      // its centre: near white
uniform float intensity = 1.15;
uniform float opacity : hint_range(0.0, 1.0) = 0.75;
uniform float wave : hint_range(0.0, 0.3) = 0.07;        // sideways wavering (UV), growing with height
uniform float wave_speed = 2.4;
uniform float ripple_speed = 1.6;                        // the upward ripple of the body
uniform float grow_time : hint_range(0.02, 0.5) = 0.18;  // fraction of the life to grow up out of the goop
uniform float die_from : hint_range(0.3, 1.0) = 0.65;    // fraction of the life where it starts fading

varying float age;
varying float inst;

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
    // a cylindrical billboard: upright, turned to face the camera, keeping the instance's width (x scale) and height (y scale)
    float w = length(MODEL_MATRIX[0].xyz);
    float h = length(MODEL_MATRIX[1].xyz);
    vec3 pos = MODEL_MATRIX[3].xyz;
    vec3 to_cam = INV_VIEW_MATRIX[3].xyz - pos;
    vec3 fwd = normalize(vec3(to_cam.x, 0.0, to_cam.z) + vec3(0.0, 0.0, 1e-4));
    vec3 right = normalize(cross(vec3(0.0, 1.0, 0.0), fwd));
    mat4 m = mat4(vec4(right * w, 0.0), vec4(0.0, h, 0.0, 0.0), vec4(fwd, 0.0), vec4(pos, 1.0));
    MODELVIEW_MATRIX = VIEW_MATRIX * m;
    MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
    age = INSTANCE_CUSTOM.x;
    inst = INSTANCE_CUSTOM.y * 17.0;
    // it grows up out of the goop, then stands; a slow breathing of the height
    float grow = smoothstep(0.0, grow_time, age);
    float breathe = 1.0 + 0.06 * sin(TIME * 3.1 + inst);
    VERTEX.y = (VERTEX.y + 0.5) * grow * breathe; // the base on the paint
}

void fragment() {
    vec2 uv = UV;
    float hgt = 1.0 - uv.y;          // 0 at the base, 1 at the top
    float n = vnoise(vec2(inst, hgt * 2.5 - TIME * ripple_speed)) - 0.5;
    uv.x += (sin(TIME * wave_speed + hgt * 3.5 + inst) * 0.6 + n * 0.8) * wave * hgt;
    vec4 c1 = texture(flame_tex, clamp(uv, 0.0, 1.0));
    // a second sample, narrower and out of phase: the body flickers
    vec2 uv2 = vec2((uv.x - 0.5) * 1.25 + 0.5 + n * 0.05, uv.y + 0.04 * sin(TIME * 5.3 + inst + 1.7));
    vec4 c2 = texture(flame_tex, clamp(uv2, 0.0, 1.0));
    float a = max(c1.a, c2.a * 0.8);
    float lum = max(dot(c1.rgb, vec3(0.333)), dot(c2.rgb, vec3(0.333)) * 0.8);
    // the body ripples upward; the top dissolves
    float ripple = 0.75 + 0.25 * vnoise(vec2(inst + 3.0, hgt * 4.0 - TIME * ripple_speed * 1.3));
    float top = 1.0 - smoothstep(0.7, 1.0, hgt) * 0.6;
    float fade = 1.0 - smoothstep(die_from, 1.0, age);
    float v = a * lum * ripple * top * fade * opacity;
    if (v <= 0.01) {
        discard;
    }
    // the slit (the brightest part of the sprite) reads near white, the glow pale yellow
    vec3 col = mix(flame_color.rgb, core_color.rgb, smoothstep(0.55, 1.0, lum));
    ALBEDO = col * intensity * v;
    ALPHA = v;
}
