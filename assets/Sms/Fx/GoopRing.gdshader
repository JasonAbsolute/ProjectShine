// The ripple of a goop bubble's pop (SmsPollutionLayer's GoopRings): a soft ring drawn into the quad from its UV, in the goop's own
// colour (goop_albedo by world XZ, lightened by `brighten`), fading with INSTANCE_CUSTOM.x = the ring's age 0..1. Nothing outside the
// band is drawn, so the quad never shows.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded;

uniform sampler2D goop_albedo : source_color, hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float albedo_tiles_per_metre = 0.07;
uniform float opacity : hint_range(0.0, 1.0) = 0.6;      // the ring's opacity when it forms
uniform float brighten : hint_range(0.0, 1.0) = 0.25;    // how much lighter than the paint (0 = the paint's colour)
uniform float band_width : hint_range(0.02, 0.5) = 0.18; // the ring's thickness as a fraction of its radius

varying vec3 world_pos;
varying float age;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    age = INSTANCE_CUSTOM.x;
}

void fragment() {
    float r = length(UV - vec2(0.5)) * 2.0;                       // 0 at the centre, 1 at the quad's edge
    float inner = 1.0 - band_width;
    float band = smoothstep(inner - 0.08, inner + 0.04, r) * (1.0 - smoothstep(0.9, 1.0, r));
    float a = band * opacity * (1.0 - age);
    if (a <= 0.004) {
        discard;
    }
    vec3 goop = texture(goop_albedo, world_pos.xz * albedo_tiles_per_metre).rgb;
    ALBEDO = mix(goop, vec3(1.0), brighten);
    ALPHA = a;
}
