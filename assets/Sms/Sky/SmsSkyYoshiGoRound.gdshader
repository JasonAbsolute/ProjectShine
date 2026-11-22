shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque, skip_vertex_transform, fog_disabled;

// sirena_ex1/map/map/sky.bmd -- The Yoshi-Go-Round's Secret's sunflower sky: five static materials (the dome _VRsph, the sunflowers _himawari / _1 / _3,
// the clouds _kumo), no BTK / BRK, drawn round the camera like TSky. The flower and cloud sheets use the game's 128 / 255 alpha cut.
// 243b: these materials have LIGHTING ON (colorChan: matSrc vertex, ambSrc register, litMask lights 0 + 1, DF_CLAMP): the colour is texture x vertex
// colour x clamp(ambient + sum max(0, N . L) x light), per vertex, with the dome's INWARD normals. The stage's lights (scene.bin, the object set):
// the object set's ambient (128, 128, 128) and its sun (255, 255, 255) at (200000, 500000, 200000), GX light 0 by TLightCommon::setLight, no
// attenuation (the sub / shadow / specular entries never reach the sky). Over most of the dome the sun is behind the surface, so the bright sheet (5, 158, 229) shows at about half -- the game's deep blue. The maths runs in the game's
// own 8-bit colour space (the sheet sampled raw) and is decoded to linear once at the end (243's double decode read it as a saturated maze pattern).
uniform sampler2D albedo_tex : filter_linear_mipmap, repeat_enable;
uniform vec3 material_color = vec3(1.0);
uniform bool alpha_cut = false;
uniform float alpha_threshold = 0.5019608;
uniform float brightness = 1.0;
uniform bool stage_lit = true;                          // the game's lighting on the sky; off = the raw sheet x vertex colour
uniform vec3 ambient_light = vec3(0.5019608);           // (128, 128, 128)
uniform vec3 sun_color = vec3(1.0);                     // (255, 255, 255)
uniform vec3 sun_position = vec3(200000.0, 500000.0, 200000.0);     // game units, the dome's own frame (radius 90000)

varying vec2 v_uv;
varying vec4 v_color;
varying vec3 v_light;

void vertex() {
    vec3 light = vec3(1.0);
    if (stage_lit) {
        vec3 n = normalize(NORMAL);
        light = ambient_light;
        light += max(dot(n, normalize(sun_position - VERTEX)), 0.0) * sun_color;
        light = clamp(light, 0.0, 1.0);
    }
    v_light = light;
    vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz - MODEL_MATRIX[3].xyz + CAMERA_POSITION_WORLD;
    VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
    v_uv = UV;
    v_color = COLOR;
}

vec3 srgb_to_linear(vec3 c) {
    return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
    vec4 sample_color = texture(albedo_tex, v_uv);
    float source_alpha = sample_color.a * v_color.a;
    if (alpha_cut && source_alpha < alpha_threshold) discard;
    vec3 game_rgb = clamp(sample_color.rgb * v_color.rgb * material_color * v_light * brightness, 0.0, 1.0);
    ALBEDO = srgb_to_linear(game_rgb);
    ALPHA = 1.0;
}
