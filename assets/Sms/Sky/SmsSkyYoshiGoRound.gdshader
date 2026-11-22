shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque, skip_vertex_transform, fog_disabled;

// sirena_ex1/map/map/sky.bmd: the Yoshi's Island sunflower sky follows the camera like TSky.
// Every surface is texture × vertex colour. The flower and cloud sheets use the game's 128/255 alpha cut.
uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform vec3 material_color = vec3(1.0);
uniform bool alpha_cut = false;
uniform float alpha_threshold = 0.5019608;
uniform float brightness = 1.0;

varying vec2 v_uv;
varying vec4 v_color;

void vertex() {
    vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz - MODEL_MATRIX[3].xyz + CAMERA_POSITION_WORLD;
    VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
    v_uv = UV;
    v_color = COLOR;
}

void fragment() {
    vec4 sample_color = texture(albedo_tex, v_uv);
    float source_alpha = sample_color.a * v_color.a;
    if (alpha_cut && source_alpha < alpha_threshold) discard;
    vec3 game_rgb = clamp(sample_color.rgb * v_color.rgb * material_color * brightness, 0.0, 1.0);
    ALBEDO = pow(game_rgb, vec3(2.2));
    ALPHA = 1.0;
}
