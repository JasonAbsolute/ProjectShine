// The goop surface of an SmsPollutionLayer (TPollutionLayer's model: its I8 texture is the paint map). The layer sets pollution_map,
// layer_min, layer_inv_size and threshold; the look is yours -- the colours, an albedo texture, the flow. The paint is read by WORLD XZ
// (as the game samples it), so any mesh shape over the floor works, and the goop appears exactly where Mario's feet find it. The game's
// goop textures (assets/Sms/Fx/Goop: Brown = the ordinary goop, Oil, Electric, Fire, Pink, Beta) tile by world XZ as well.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D pollution_map : filter_linear, repeat_disable, hint_default_black; // set by SmsPollutionLayer (R8: 0 clean .. 255)
uniform vec2 layer_min = vec2(0.0);                   // set by SmsPollutionLayer: the layer's -X / -Z corner (world metres)
uniform vec2 layer_inv_size = vec2(1.0);              // set by SmsPollutionLayer: 1 / (texels x texel size) (world metres)
uniform float threshold = 0.1176;                     // set by SmsPollutionLayer: mPollutedThreshold / 255 (30 for ordinary goop)
uniform float edge_softness : hint_range(0.0, 1.0) = 0.3; // how far above the threshold the goop reaches full opacity
uniform vec4 goop_color : source_color = vec4(0.20, 0.12, 0.05, 1.0);
uniform vec4 goop_color_dark : source_color = vec4(0.06, 0.035, 0.015, 1.0);
uniform sampler2D goop_albedo : source_color, hint_default_white, filter_linear_mipmap, repeat_enable; // the goop texture (SmsPollutionLayer sets the type's default from assets/Sms/Fx/Goop)
uniform float albedo_tiles_per_metre = 0.07;        // one repeat of the 250 px swirl every ~14 m (smaller = bigger swirls); 0.5 looked like wallpaper, 0.15 still repeated
uniform float detail_mix : hint_range(0.0, 1.0) = 0.25; // a second sample, 2.3 x finer and turned, blended in: crispness up close, and the big repeat never reads as a grid
uniform float warp_amount = 0.5;                      // metres the surface wobbles (the game's goop crawls and ripples); 0 = still
uniform float warp_speed = 0.2;
uniform float blotch_mix : hint_range(0.0, 1.0) = 1.0; // 0 = the texture as painted (set by the layer when it has one), 1 = tinted by the procedural blotches
uniform float noise_scale = 0.6;                      // the blotches, per metre
uniform float flow_speed = 0.06;                      // the slow crawl of the surface (metres a second)
uniform float roughness : hint_range(0.0, 1.0) = 0.22;
uniform float specular : hint_range(0.0, 1.0) = 0.6;
uniform float rim_darken : hint_range(0.0, 1.0) = 0.45; // the thin paint at the edge is darker

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

void fragment() {
    vec2 uv = (world_pos.xz - layer_min) * layer_inv_size;
    float m = texture(pollution_map, uv).r;
    float a = smoothstep(threshold, threshold + edge_softness * (1.0 - threshold), m);
    if (a <= 0.003) {
        discard;
    }
    vec2 base = world_pos.xz;
    vec2 wobble = vec2(fbm(base * 0.3 + vec2(TIME * warp_speed, 0.0)), fbm(base * 0.3 + vec2(3.7, -TIME * warp_speed))) - vec2(0.5);
    vec2 drift = vec2(TIME * flow_speed, -TIME * flow_speed * 0.6);
    vec2 uv_a = (base + wobble * warp_amount + drift) * albedo_tiles_per_metre;
    vec2 p_b = base * 2.3 + wobble * warp_amount * 1.6 - drift;
    vec2 uv_b = vec2(p_b.y, -p_b.x) * albedo_tiles_per_metre; // turned a quarter so the two copies never line up
    vec3 albedo = mix(texture(goop_albedo, uv_a).rgb, texture(goop_albedo, uv_b).rgb, detail_mix);
    vec3 col = albedo;
    if (blotch_mix > 0.0) {
        float n = fbm(base * noise_scale + drift * 1.3);
        vec3 blotch = mix(goop_color_dark.rgb, goop_color.rgb, n);
        col = mix(albedo, albedo * blotch, blotch_mix);
    }
    float rim = smoothstep(threshold, min(1.0, threshold + 2.0 * edge_softness), m);
    col *= mix(1.0 - rim_darken, 1.0, rim);
    ALBEDO = col;
    ALPHA = a;
    ROUGHNESS = roughness;
    SPECULAR = specular;
}
