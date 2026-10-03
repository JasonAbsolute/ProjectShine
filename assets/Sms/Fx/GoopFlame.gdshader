// A flame on fire goop (SmsPollutionLayer's GoopFlames, standing in for MAP_POLLUTION_MS_NEWFIRE_B / _A). From his reference (Pianta Village,
// The Goopy Inferno): tall, thin, pale yellow-white TRANSLUCENT wisps standing on the goop -- a wavering column with a secondary tongue,
// their tips dissolving. Procedural, on an upright billboard (the instance's x scale = width, y scale = height): three thin soft streaks
// rising from one spot, swaying more toward their tips and rippling upward, a ragged torn top, a soft warm glow at the foot; additive.
// INSTANCE_CUSTOM.x = the wisp's age 0..1 (it grows up out of the goop, burns, fades), .y = a per-wisp random 0..1.
// tools/projectshine/wisp_preview.py renders this same math to a PNG for a look without the game.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded;

uniform vec4 outer_color : source_color = vec4(1.0, 0.8, 0.42, 1.0);   // the streaks' edges: pale orange-yellow
uniform vec4 core_color : source_color = vec4(1.0, 0.98, 0.85, 1.0);   // their centres: near white
uniform float intensity = 1.0;
uniform float opacity : hint_range(0.0, 1.0) = 0.7;
uniform float wave : hint_range(0.0, 0.6) = 0.3;          // sideways swaying of the streaks (fraction of the width), growing with height
uniform float wave_speed = 2.4;
uniform float ripple_speed = 1.6;                         // the upward ripple of the bodies
uniform float foot_glow : hint_range(0.0, 0.6) = 0.22;    // the soft glow where the wisp meets the goop
uniform float grow_time : hint_range(0.02, 0.5) = 0.18;   // fraction of the life to grow up out of the goop
uniform float die_from : hint_range(0.3, 1.0) = 0.65;     // fraction of the life where it starts fading

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

// one streak: its contribution to the colour sum and the value sum
void streak(float x, float h, float xo, float hs, float wgt, float ph, float thin, float lobe, inout vec3 col, inout float vt) {
    float hh = h / hs;
    if (hh > 1.0) {
        return;
    }
    float shape = pow(1.0 - hh, 0.8) * smoothstep(0.0, 0.05, hh) * (0.6 + 0.4 * sin(3.14159265 * pow(hh, 0.9)));
    float hw = max(0.5 * shape * thin, 1e-3);
    float n = vnoise(vec2(inst + lobe * 7.0, hh * 2.5 - TIME * ripple_speed)) - 0.5;
    float sway = (sin(TIME * wave_speed + hh * 4.0 + inst + ph) * 0.6 + n * 1.2 + sin(hh * 9.0 + TIME * 1.1 + ph) * 0.25) * wave * pow(hh, 1.2);
    float xc = xo * (0.3 + 0.45 * hh) + sway;
    float d = abs(x - xc) / hw;
    float body = exp(-d * d * 1.3) * smoothstep(1.8, 0.9, d);
    float ripple = 0.5 + 0.5 * vnoise(vec2(inst + 3.0 + lobe, hh * 5.0 - TIME * ripple_speed * 1.3));
    float tear = (vnoise(vec2(inst + 11.0 + lobe * 3.0, hh * 6.0 - TIME * 2.0)) - 0.5) * 0.25;
    float top = smoothstep(1.0, 0.65, hh + tear);
    float v = body * ripple * top * wgt;
    float core = exp(-d * d * 5.0);
    col += mix(outer_color.rgb, core_color.rgb, core) * v;
    vt += v;
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
    VERTEX.y += 0.5; // the base on the paint
}

void fragment() {
    float x = UV.x - 0.5;            // -0.5 .. 0.5 across
    float hgt = 1.0 - UV.y;          // 0 at the base, 1 at the top
    float grow = smoothstep(0.0, grow_time, age);
    float fade = 1.0 - smoothstep(die_from, 1.0, age);
    float h = hgt / max(grow, 1e-3); // it grows up out of the goop
    if (h > 1.0) {
        discard;
    }
    vec3 col = vec3(0.0);
    float vt = 0.0;
    // three thin streaks rising from one spot: the column, and two shorter tongues beside it, out of phase
    streak(x, h, 0.0, 1.0, 1.0, 0.0, 0.5, 0.0, col, vt);
    streak(x, h, -0.16 + 0.1 * sin(inst), 0.78, 0.65, 2.1, 0.42, 1.0, col, vt);
    streak(x, h, 0.17 + 0.08 * cos(inst * 1.3), 0.58, 0.5, 4.0, 0.38, 2.0, col, vt);
    // the soft warm glow at the foot
    float g = exp(-((x / 0.5) * (x / 0.5) * 3.0 + ((hgt - 0.1) / 0.22) * ((hgt - 0.1) / 0.22))) * foot_glow * smoothstep(0.0, 0.02, hgt) * grow;
    col += outer_color.rgb * g;
    vt += g;
    if (vt <= 1e-4) {
        discard;
    }
    col /= vt;
    float v = min(1.0, vt) * fade * opacity;
    if (v <= 0.005) {
        discard;
    }
    ALBEDO = col * intensity * v;
    ALPHA = v;
}
