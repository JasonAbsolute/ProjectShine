shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, skip_vertex_transform, fog_disabled;

// coro_ex4 sky.bmd has four static materials and no BTK/BRK animation files.
uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;   // every sheet is wrap REPEAT in sky.bmd (the trees run u 1..8, the hedge 1..12 round the dome); clamped, they sampled a transparent edge and the alpha cut removed them
uniform bool alpha_cut = false;
uniform float alpha_threshold = 0.5019608;
uniform float brightness = 1.0;
varying vec2 v_uv;
varying vec4 v_color;

void vertex() {
    vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz - MODEL_MATRIX[3].xyz + CAMERA_POSITION_WORLD;
    VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
    NORMAL = normalize((VIEW_MATRIX * vec4((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz, 0.0)).xyz);
    v_uv = UV;
    v_color = COLOR;
}

void fragment() {
    vec4 texel = texture(albedo_tex, v_uv) * v_color;
    if (alpha_cut && texel.a < alpha_threshold) discard;
    ALBEDO = pow(clamp(texel.rgb * brightness, 0.0, 1.0), vec3(2.2));
    ALPHA = 1.0;
}
