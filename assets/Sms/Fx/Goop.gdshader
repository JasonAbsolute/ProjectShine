// The goop surface of an SmsPollutionLayer (TPollutionLayer's model: its I8 texture is the paint map). The layer sets pollution_map,
// layer_min, layer_inv_size and threshold; the look is yours -- the colours, an albedo texture, the flow. The paint is read by WORLD XZ
// (as the game samples it), so any mesh shape over the floor works, and the goop appears exactly where Mario's feet find it.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D pollution_map : filter_linear, repeat_disable, hint_default_black; // set by SmsPollutionLayer (R8: 0 clean .. 255)
uniform vec2 layer_min = vec2(0.0);                   // set by SmsPollutionLayer: the layer's -X / -Z corner (world metres)
uniform vec2 layer_inv_size = vec2(1.0);              // set by SmsPollutionLayer: 1 / (texels x texel size) (world metres)
uniform float threshold = 0.1176;                     // set by SmsPollutionLayer: mPollutedThreshold / 255 (30 for ordinary goop)
uniform float edge_softness : hint_range(0.0, 1.0) = 0.3; // how far above the threshold the goop reaches full opacity
uniform vec4 goop_color : source_color = vec4(0.20, 0.12, 0.05, 1.0);
uniform vec4 goop_color_dark : source_color = vec4(0.06, 0.035, 0.015, 1.0);
uniform sampler2D goop_albedo : source_color, hint_default_white, filter_linear_mipmap, repeat_enable; // optional: the game's goop texture
uniform float albedo_tiles_per_metre = 0.25;
uniform float noise_scale = 0.6;                      // the blotches, per metre
uniform float flow_speed = 0.04;                      // the slow crawl of the blotches
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
    vec2 flow = world_pos.xz * noise_scale + vec2(TIME * flow_speed, -TIME * flow_speed * 0.7);
    float n = fbm(flow);
    vec3 col = mix(goop_color_dark.rgb, goop_color.rgb, n) * texture(goop_albedo, world_pos.xz * albedo_tiles_per_metre).rgb;
    float rim = smoothstep(threshold, min(1.0, threshold + 2.0 * edge_softness), m);
    col *= mix(1.0 - rim_darken, 1.0, rim);
    ALBEDO = col;
    ALPHA = a;
    ROUGHNESS = roughness;
    SPECULAR = specular;
}
