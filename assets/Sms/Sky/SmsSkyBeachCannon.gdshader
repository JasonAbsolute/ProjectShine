shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, skip_vertex_transform, fog_disabled;

// coro_ex4/map/map/sky.bmd -- The Beach Cannon's Secret's sunflower sky: four static materials (the dome _VRsph, the trees _himawari, the clouds _kumo,
// the hedge _yama), no BTK / BRK, drawn round the camera like TSky. Every sheet is wrap REPEAT (the trees run u 1..8, the hedge 1..12 round the dome).
// 243b: unlike the other secret skies, these materials have LIGHTING ON (colorChan: matSrc vertex, ambSrc register, litMask lights 0 + 1, DF_CLAMP):
// the colour is texture x vertex colour x clamp(ambient + sum max(0, N . L) x light), per vertex, with the dome's INWARD normals. The stage's lights
// (scene.bin LightAry / AmbAry, the object set): the object set's ambient (128, 128, 128) and its sun (255, 255, 255) at (200000, 500000, 200000), loaded into
// GX light 0 by TLightCommon::setLight with no attenuation (the sub / shadow / specular entries never reach the sky). Over most of the dome the sun is behind the surface, so the sheet shows at about half: the game's
// khaki (127, 126, 95) is the sheet (246, 248, 184) x 0.51. The maths runs in the game's own 8-bit colour space (the sheet sampled raw, no source_color)
// and is decoded to linear ONCE at the end, so the screen shows the game's values (243's double decode read the pale yellow as yellow-green).
uniform sampler2D albedo_tex : filter_linear_mipmap, repeat_enable;
uniform bool alpha_cut = false;
uniform float alpha_threshold = 0.5019608;              // the game's 128 / 255 alpha test on the trees, clouds and hedge
uniform float brightness = 1.0;
uniform bool stage_lit = true;                          // the game's lighting on the sky; off = the raw sheet x vertex colour
uniform vec3 ambient_light = vec3(0.5019608);           // (128, 128, 128)
uniform vec3 sun_color = vec3(1.0);                     // (255, 255, 255)
uniform vec3 sun_position = vec3(200000.0, 500000.0, 200000.0);     // game units, the dome's own frame (radius 90000)
varying vec2 v_uv;
varying vec4 v_color;
varying vec3 v_light;

void vertex() {
    // the GX colour channel, per vertex, in the model's (game-unit) frame before the camera follow moves it
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
    NORMAL = normalize((VIEW_MATRIX * vec4((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz, 0.0)).xyz);
    v_uv = UV;
    v_color = COLOR;
}

vec3 srgb_to_linear(vec3 c) {
    return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
    vec4 texel = texture(albedo_tex, v_uv);
    if (alpha_cut && texel.a * v_color.a < alpha_threshold) discard;
    vec3 game_rgb = clamp(texel.rgb * v_color.rgb * v_light * brightness, 0.0, 1.0);
    ALBEDO = srgb_to_linear(game_rgb);
    ALPHA = 1.0;
}
