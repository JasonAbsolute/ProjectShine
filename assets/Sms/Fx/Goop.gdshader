// The goop surface of an SmsPollutionLayer (TPollutionLayer's model: its I8 texture is the paint map). The layer sets pollution_map,
// layer_to_local, layer_min, layer_inv_size and threshold; the look is yours -- the texture, the colours, the flow. The paint and the
// swirl are read in the LAYER'S FRAME (world position taken through layer_to_local, the same frame Mario's feet are sampled in), so a
// turned or scaled quad carries its goop with it. The game's goop textures (assets/Sms/Fx/Goop: Brown = the ordinary goop, Oil, Electric,
// Fire, Pink, Beta) tile by that frame as well.
// THE FLOW: the game animates the goop's texture coordinates (a BTK plus an indirect warp) so the swirls creep like wet paint. Here the
// texture is advected along a slowly varying direction field -- two phases, each sliding then resetting, cross-faded so the reset never
// shows (the flow-map technique) -- with a quicker wobble on top. The surface is glossy paint, not water: the ripple normals and the
// sheen are kept small by default.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D pollution_map : filter_linear, repeat_disable, hint_default_black; // set by SmsPollutionLayer (R8: 0 clean .. 255)
uniform mat4 layer_to_local = mat4(1.0);               // set by SmsPollutionLayer: world -> the layer's frame
uniform vec2 layer_min = vec2(0.0);                   // set by SmsPollutionLayer: the layer's -X / -Z corner (its frame, metres)
uniform vec2 layer_inv_size = vec2(1.0);              // set by SmsPollutionLayer: 1 / (texels x texel size) (metres)
uniform float threshold = 0.1176;                     // set by SmsPollutionLayer: mPollutedThreshold / 255 (30 for ordinary goop)
uniform float edge_softness : hint_range(0.0, 1.0) = 0.3; // how far above the threshold the goop reaches full opacity

uniform sampler2D goop_albedo : source_color, hint_default_white, filter_linear_mipmap, repeat_enable; // the goop texture (the layer sets the type's default)
uniform float albedo_tiles_per_metre = 0.07;        // one repeat of the 250 px swirl every ~14 m (smaller = bigger swirls)
uniform float detail_mix : hint_range(0.0, 1.0) = 0.25; // a second sample, 2.3 x finer and turned, blended in: crispness up close, no visible repeat

// the paint flow
uniform float flow_speed = 0.22;                      // metres a second the swirl slides along the flow field
uniform float flow_cycle = 2.5;                       // seconds a phase slides before it resets (cross-faded with the other phase)
uniform float flow_dir_scale = 0.12;                  // the direction field's scale, per metre (smaller = broader currents)
uniform float warp_amount = 0.6;                      // metres the surface wobbles on top of the flow; 0 = none
uniform float warp_speed = 0.45;
uniform float blotch_mix : hint_range(0.0, 1.0) = 1.0; // 0 = the texture as painted (set by the layer when it has one), 1 = tinted by the procedural blotches
uniform float noise_scale = 0.6;                      // the blotches, per metre
uniform vec4 goop_color : source_color = vec4(0.20, 0.12, 0.05, 1.0);
uniform vec4 goop_color_dark : source_color = vec4(0.06, 0.035, 0.015, 1.0);
// the texture's tone: the flow blends two copies of the swirl, which averages its tones toward the middle and reads light and washed;
// these put the body back (the game's goop sits dark and saturated under its flat lighting)
uniform float brightness : hint_range(0.2, 1.5) = 0.78;
uniform float contrast : hint_range(0.5, 2.0) = 1.25;
uniform float saturation : hint_range(0.0, 2.0) = 1.15;

// the surface: glossy paint; the ripple normals and the sheen are the "water on top" -- small by default, yours to raise
uniform float ripple_strength : hint_range(0.0, 2.0) = 0.2;
uniform float ripple_scale = 1.6;                            // ripples per metre
uniform float ripple_speed = 0.35;
uniform float roughness : hint_range(0.0, 1.0) = 0.18;
uniform float specular : hint_range(0.0, 1.0) = 0.4;
uniform float sheen : hint_range(0.0, 1.0) = 0.0;            // the pale gloss at grazing angles (off: it lightened the paint)
uniform vec4 sheen_color : source_color = vec4(0.9, 0.95, 1.0, 1.0);
uniform float rim_darken : hint_range(0.0, 1.0) = 0.45;     // the thin paint at the goop's edge is darker

varying vec3 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float vnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
    float v = 0.0;
    float amp = 0.5;
    for (int k = 0; k < 4; k++) {
        v += amp * vnoise(p);
        p = p * 2.03 + vec2(17.1, 9.7);
        amp *= 0.5;
    }
    return v;
}

float ripples(vec2 p) {
    float t = TIME * ripple_speed;
    return fbm(p * ripple_scale + vec2(t, -0.6 * t)) + 0.6 * fbm(p * ripple_scale * 2.3 + vec2(-0.7 * t, 0.9 * t) + 5.3);
}

// the swirl texture at a surface point, slid along the flow field: two phases a half cycle apart, cross-faded
vec3 flowing_albedo(vec2 p, vec2 dir, vec2 wobble) {
    float ph1 = fract(TIME / flow_cycle);
    float ph2 = fract(ph1 + 0.5);
    float slide = flow_speed * flow_cycle;                // metres one phase travels before it resets
    vec2 off1 = dir * (ph1 - 0.5) * slide;
    vec2 off2 = dir * (ph2 - 0.5) * slide;
    float w = smoothstep(0.45, 1.0, abs(2.0 * ph1 - 1.0));  // phase 1 pure for 45 % of its cycle, then faded into phase 2 before it resets (a short blend keeps the swirl crisp)
    vec2 q1 = p + off1 + wobble;
    vec2 q2 = p + off2 + wobble * 0.7;
    vec2 uv_a1 = q1 * albedo_tiles_per_metre;
    vec2 uv_a2 = q2 * albedo_tiles_per_metre;
    vec2 r1 = q1 * 2.3;
    vec2 r2 = q2 * 2.3;
    vec2 uv_b1 = vec2(r1.y, -r1.x) * albedo_tiles_per_metre; // the finer copy, turned a quarter so the two never line up
    vec2 uv_b2 = vec2(r2.y, -r2.x) * albedo_tiles_per_metre;
    vec3 c1 = mix(texture(goop_albedo, uv_a1).rgb, texture(goop_albedo, uv_b1).rgb, detail_mix);
    vec3 c2 = mix(texture(goop_albedo, uv_a2).rgb, texture(goop_albedo, uv_b2).rgb, detail_mix);
    return mix(c1, c2, w);
}

void fragment() {
    vec3 lp = (layer_to_local * vec4(world_pos, 1.0)).xyz;
    vec2 base = lp.xz;
    vec2 uv = (base - layer_min) * layer_inv_size;
    float m = texture(pollution_map, uv).r;
    float a = smoothstep(threshold, threshold + edge_softness * (1.0 - threshold), m);
    if (a <= 0.003) {
        discard;
    }
    // the flow field: broad currents that turn across the surface (a slowly drifting noise direction), plus a quicker wobble
    vec2 dir = normalize(vec2(fbm(base * flow_dir_scale + TIME * 0.01) - 0.5, fbm(base * flow_dir_scale + vec2(31.7, 12.9) - TIME * 0.008) - 0.5) + vec2(1e-3, 0.0));
    vec2 wobble = (vec2(fbm(base * 0.3 + vec2(TIME * warp_speed, 0.0)), fbm(base * 0.3 + vec2(3.7, -TIME * warp_speed))) - vec2(0.5)) * warp_amount;
    vec3 albedo = flowing_albedo(base, dir, wobble);
    float lum = dot(albedo, vec3(0.299, 0.587, 0.114));
    albedo = mix(vec3(lum), albedo, saturation);
    albedo = clamp(((albedo - 0.5) * contrast + 0.5) * brightness, 0.0, 1.0);
    vec3 col = albedo;
    if (blotch_mix > 0.0) {
        float n = fbm(base * noise_scale + wobble * 0.5);
        vec3 blotch = mix(goop_color_dark.rgb, goop_color.rgb, n);
        col = mix(albedo, albedo * blotch, blotch_mix);
    }
    float rim = smoothstep(threshold, min(1.0, threshold + 2.0 * edge_softness), m);
    col *= mix(1.0 - rim_darken, 1.0, rim);

    vec3 n_view = NORMAL;
    if (ripple_strength > 0.0) {
        float e = 0.04;
        float h0 = ripples(base);
        float gx = (ripples(base + vec2(e, 0.0)) - h0) / e;
        float gz = (ripples(base + vec2(0.0, e)) - h0) / e;
        n_view = normalize(NORMAL + mat3(VIEW_MATRIX) * (vec3(-gx, 0.0, -gz) * ripple_strength * 0.12));
        NORMAL = n_view;
    }
    if (sheen > 0.0) {
        float grazing = pow(1.0 - clamp(dot(n_view, normalize(VIEW)), 0.0, 1.0), 3.0);
        col = mix(col, sheen_color.rgb, grazing * sheen);
    }

    ALBEDO = col;
    ALPHA = a;
    ROUGHNESS = roughness;
    SPECULAR = specular;
}
