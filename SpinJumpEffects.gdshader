shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, blend_add;

uniform sampler2D puff_tex : source_color;
uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);

uniform float spin_speed = 10.0;     // fast internal motion (not object rotation)
uniform float tile_u = 3.0;         // number of puffs around the ring
uniform float alpha = 1.0;           // overall intensity

// Ring shape (UV-space donut)
uniform float ring_radius = 0.7;     // where the ring sits (0..~1)
uniform float ring_thickness = 0.42;  // band thickness
uniform float ring_softness = 0.04;   // feather edges

void fragment() {
    // UV center at (0.5, 0.5)
    vec2 p = UV - vec2(0.5);

    // Radius in "donut space"
    float r = length(p) * 2.0;

    float half_t = ring_thickness * 0.5;

    // Donut mask: 1 in the band, 0 inside hole and outside ring
    float inner = smoothstep(ring_radius - half_t - ring_softness, ring_radius - half_t, r);
    float outer = 1.0 - smoothstep(ring_radius + half_t, ring_radius + half_t + ring_softness, r);
    float ring_mask = inner * outer;

    // Polar angle around center -> u
    float ang = atan(p.y, p.x);                 // -pi..pi
    float u = (ang / 6.2831853) + 0.5;          // 0..1 around circle
    float u_tiles = u * tile_u + TIME * spin_speed;

    // IMPORTANT: sample inside each tile so you get repeated puffs
    float tile_x = fract(u_tiles);

    // v goes across ring thickness (0..1)
    float v = clamp((r - (ring_radius - half_t)) / max(ring_thickness, 0.0001), 0.0, 1.0);

    vec4 tex = texture(puff_tex, vec2(tile_x, v));

    // View-angle fade: visible from above, fades at side view
    float ndv = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);

    float a = tex.a * ring_mask * alpha;

    ALBEDO = tex.rgb * tint.rgb * a;
    ALPHA = a;
}
