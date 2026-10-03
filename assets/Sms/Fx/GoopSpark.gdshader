// A bolt on electric goop (SmsPollutionLayer's GoopSparks, standing in for MAP_POLLUTION_MS_THUNDER_S): an upright billboard quad on
// which a jagged lightning bolt CLIMBS from the paint to the top of the quad (revealed bottom-up over the first 45 % of its life), with a
// fork, a bright white core in a cyan glow, flickering, then fading. INSTANCE_CUSTOM.x = the bolt's age 0..1. The instance's x scale
// is the quad's width, its y scale the bolt's height.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded;

uniform vec4 glow_color : source_color = vec4(0.45, 0.85, 1.0, 1.0);
uniform vec4 core_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float intensity = 1.8;
uniform float climb_time : hint_range(0.1, 1.0) = 0.35;   // the fraction of the life the bolt takes to reach the top
uniform float jag : hint_range(0.0, 1.0) = 0.35;          // how far the bolt wanders sideways (fraction of the quad's half width)
uniform float flicker_rate = 30.0;
uniform float lean_max : hint_range(0.0, 1.2) = 0.56;      // radians: each bolt leans a random angle up to this off vertical (the layer sets it from SparkLean)
uniform float thickness : hint_range(0.5, 3.0) = 2.0;    // the core's width (1 = the first, hair-thin bolt)

varying float age;
varying float inst; // INSTANCE_ID is a vertex built-in: carried over for the per-instance variety

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

// linear (not smoothed) noise: angular, the way lightning bends
float lnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash21(i), b = hash21(i + vec2(1.0, 0.0)), c = hash21(i + vec2(0.0, 1.0)), d = hash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// the bolt's sideways offset at a height (-0.5 .. 0.5 of the quad), for a seed
float bolt_x(float hgt, float seed) {
    float o = (lnoise(vec2(hgt * 7.0 + seed, seed * 1.3)) - 0.5) * 0.7
            + (lnoise(vec2(hgt * 19.0 + seed * 2.1, 4.0 + seed)) - 0.5) * 0.3;
    return o * jag;
}

void vertex() {
    // a cylindrical billboard: upright, turned to face the camera, keeping the instance's width (x scale) and height (y scale)
    float w = length(MODEL_MATRIX[0].xyz);
    float h = length(MODEL_MATRIX[1].xyz);
    vec3 pos = MODEL_MATRIX[3].xyz;
    vec3 to_cam = INV_VIEW_MATRIX[3].xyz - pos;
    vec3 fwd = normalize(vec3(to_cam.x, 0.0, to_cam.z) + vec3(0.0, 0.0, 1e-4));
    vec3 right = normalize(cross(vec3(0.0, 1.0, 0.0), fwd));
    // the bolt leans a random angle off vertical, turning in the billboard's plane about its base (his ask: random angles, not straight up)
    float lean = (INSTANCE_CUSTOM.y * 2.0 - 1.0) * lean_max;
    vec3 up = vec3(0.0, 1.0, 0.0);
    vec3 r2 = right * cos(lean) + up * sin(lean);
    vec3 u2 = up * cos(lean) - right * sin(lean);
    mat4 m = mat4(vec4(r2 * w, 0.0), vec4(u2 * h, 0.0), vec4(fwd, 0.0), vec4(pos, 1.0));
    MODELVIEW_MATRIX = VIEW_MATRIX * m;
    MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
    age = INSTANCE_CUSTOM.x;
    inst = float(INSTANCE_ID) + INSTANCE_CUSTOM.y * 31.0;
    VERTEX.y += 0.5; // the quad's base on the paint
}

void fragment() {
    float x = UV.x - 0.5;            // -0.5 .. 0.5 across
    float hgt = 1.0 - UV.y;          // 0 at the base, 1 at the top
    float frame = floor(TIME * flicker_rate);
    float seed = inst * 7.31 + floor(age * 6.0) * 0.37; // the path re-jags a few times over the life
    float climb = min(1.0, age / climb_time);
    float reveal = smoothstep(climb + 0.01, climb - 0.06, hgt);          // the bolt exists only below the climbing tip
    float fade = (1.0 - smoothstep(0.55, 1.0, age)) * (0.75 + 0.25 * hash21(vec2(frame, inst)));

    // the main bolt
    float bx = bolt_x(hgt, seed);
    float d = abs(x - bx);
    float sig = 0.014 * thickness;
    float core = exp(-d * d / (2.0 * sig * sig));
    float glow = exp(-d / (0.055 * sqrt(thickness))) * 0.55;
    float v = (core + glow) * reveal;

    // a fork leaving the main bolt partway up, thinner and dimmer
    float fork_from = 0.35 + 0.3 * hash21(vec2(inst, 3.0));
    if (hgt > fork_from) {
        float side = hash21(vec2(inst, 9.0)) < 0.5 ? -1.0 : 1.0;
        float fx = bolt_x(fork_from, seed) + side * (hgt - fork_from) * 0.9 + (lnoise(vec2(hgt * 23.0 + seed, 11.0)) - 0.5) * 0.08;
        float fd = abs(x - fx);
        float fsig = 0.01 * thickness;
        float fork = (exp(-fd * fd / (2.0 * fsig * fsig)) + exp(-fd / (0.04 * sqrt(thickness))) * 0.4) * 0.7;
        v += fork * smoothstep(fork_from + 0.5, fork_from, hgt) * reveal;
    }

    // the climbing tip glows
    float tip = exp(-length(vec2(d, (hgt - climb) * 3.0)) * 22.0) * (1.0 - smoothstep(0.9, 1.0, climb));
    v += tip * 1.5;

    v *= fade;
    if (v <= 0.01) {
        discard;
    }
    vec3 col = mix(glow_color.rgb, core_color.rgb, clamp(core + tip, 0.0, 1.0));
    ALBEDO = col * intensity * v;
    ALPHA = min(1.0, v);
}
