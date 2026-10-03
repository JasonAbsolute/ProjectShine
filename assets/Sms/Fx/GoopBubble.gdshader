// A goop bubble (SmsPollutionLayer's PARTICLE_MS_RAKU_INDAWA stand-in): a half-sphere dome on the paint, wearing the goop texture under
// it by world XZ (the same tiling as Goop.gdshader), a wet rim highlight, a brightening as it is about to pop. INSTANCE_CUSTOM.x = age 0..1.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D goop_albedo : source_color, hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float albedo_tiles_per_metre = 0.07;
uniform vec4 tint : source_color = vec4(0.9, 0.9, 0.9, 1.0);   // the dome is a shade darker than the floor paint
uniform vec4 rim_color : source_color = vec4(1.0, 0.97, 0.9, 1.0);
uniform float rim_power = 3.0;
uniform float rim_strength = 0.55;
uniform float pop_flash = 0.6;                                   // how much it brightens in the last tenth of its life
uniform float roughness : hint_range(0.0, 1.0) = 0.12;
uniform float specular : hint_range(0.0, 1.0) = 0.8;

varying vec3 world_pos;
varying float age;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    age = INSTANCE_CUSTOM.x;
}

void fragment() {
    vec3 albedo = texture(goop_albedo, world_pos.xz * albedo_tiles_per_metre).rgb * tint.rgb;
    float fresnel = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), rim_power);
    float flash = smoothstep(0.88, 1.0, age) * pop_flash;
    ALBEDO = mix(albedo, rim_color.rgb, fresnel * rim_strength) + vec3(flash);
    ROUGHNESS = roughness;
    SPECULAR = specular;
}
