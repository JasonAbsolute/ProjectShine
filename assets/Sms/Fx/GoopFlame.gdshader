// A flame on fire goop (SmsPollutionLayer's GoopFlames, standing in for MAP_POLLUTION_MS_NEWFIRE_A / _B): an upright billboard quad
// (the instance's scale = its width / height) with a procedural tongue -- a noise-torn teardrop, yellow core to orange to a red edge,
// additive. INSTANCE_CUSTOM.x = the flame's age 0..1 (it grows in, then thins and fades).
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded;

uniform vec4 core_color : source_color = vec4(1.0, 0.92, 0.45, 1.0);
uniform vec4 mid_color : source_color = vec4(1.0, 0.45, 0.06, 1.0);
uniform vec4 edge_color : source_color = vec4(0.75, 0.12, 0.02, 1.0);
uniform float intensity = 1.4;
uniform float flicker_speed = 3.0;

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
    inst = float(INSTANCE_ID);
    // the quad's origin is its centre: lift it so the flame's base sits on the paint
    VERTEX.y += 0.5;
}

void fragment() {
    float x = UV.x - 0.5;            // -0.5 .. 0.5 across
    float hgt = 1.0 - UV.y;          // 0 at the base, 1 at the tip
    float t = TIME * flicker_speed + inst * 3.1;
    float n = vnoise(vec2(x * 5.0 + inst, hgt * 3.0 - t)) ;
    float n2 = vnoise(vec2(x * 11.0 + 7.3, hgt * 6.0 - t * 1.7));
    float tear = (n - 0.5) * 0.35 * hgt + (n2 - 0.5) * 0.12 * hgt;
    float width = 0.38 * (1.0 - hgt * 0.85) + 0.03;
    float d = abs(x + tear) / width;
    float grow = smoothstep(0.0, 0.12, age);
    float fade = 1.0 - smoothstep(0.55, 1.0, age);
    float tip = 0.95 * grow - (n - 0.5) * 0.25;
    float body = smoothstep(1.0, 0.55, d) * smoothstep(0.0, 0.08, hgt) * smoothstep(tip, tip - 0.3, hgt);
    float core = smoothstep(0.75, 0.0, d) * smoothstep(0.6, 0.05, hgt);
    if (body <= 0.01) {
        discard;
    }
    vec3 col = mix(edge_color.rgb, mid_color.rgb, smoothstep(1.0, 0.5, d));
    col = mix(col, core_color.rgb, core);
    ALBEDO = col * intensity * body * fade;
    ALPHA = body * fade;
}
