// ms_2d_pause_sel (the pause menu's selection sparks), pass 89 -- the .jpa's numbers (jpa_dump.py) with the JPA engine's own arithmetic:
//  BEM1: Circle r 15 (minRad 1: the rim), FixedInterval, rate 14 for maxFrame 1 -> one frame of 14 sparks at angles i x 65536 / 14
//        (JPAEmitter.cpp 416); velAxis 10 u/f along the point's own radial (JPAEmitter.cpp 554: setLength(f31_f30_f29, 10)); airResist
//        0.85 a frame; life 25 frames. The emitter ROTATION is (90, 0, -12) DEGREES (JPAEmitter.cpp 204: the shorts x 182 -> BAM; Rz Ry Rx
//        on the positions AND the velocities, calcEmitterGlobalParams) -- the circle, authored in XZ, stands up into the 2D screen's XY
//        plane and tilts like the menu pane (-12): the sparks fly OUT in every direction, a sunburst (pass 80 read the rotation as
//        0.49 degrees and laid the circle edge-on: a line of sparks sliding sideways). The emitter scale (4 x width / 129, 1, 1) is
//        applied before the rotation and to the positions only (unkFC vs unkCC): the rim is an ellipse along the item's width, the
//        speed stays 10.
//  BSP1: DirBillboard dir Vel (JPADrawVisitor.cpp 928: the quad turned to the projected velocity), 0.2 x 0.5 (x 50): 10 px across,
//        25 px along the flight; P_msm_shikaku_i (I4 16 x 16); colour TEXC x prm, blend SRCALPHA / ONE (additive).
//  ESP1: alpha 1 to 50 % of the life then -> 0; scale DISABLED (isEnableScale false: the quad keeps its size); prm colour keys
//        (255, 255, 72) @ 5 -> (255, 39, 0) @ 30 with calc 3 (Merge: the table stretched over the life) -- `ramp`.
// The node (PauseSparks.tscn): GPUParticles2D, local coords, 14 particles, one shot, explosiveness 1, fixed_fps 60 (the 2D emitter
// manager calcs SMSGetAnmFrameRate() times a frame), rotated by the menu pane's tilt by SmsPauseMenu, `rim_scale_x` set per item.
shader_type particles;
render_mode disable_force, disable_velocity;   // the drag is the game's exponential, integrated here

uniform float rim_scale_x = 4.0;      // setEmitterScale(4 x width / 129, 1, 1).x
uniform float rim_radius = 15.0;      // volume size, px (frame units)
uniform float speed = 600.0;          // 10 px a 60 Hz frame
uniform float drag = 0.85;            // airResist, per frame
uniform float count = 14.0;           // the burst
uniform float quad_across = 10.0;     // 0.2 x 50
uniform float quad_along = 25.0;      // 0.5 x 50
uniform float tex_size = 16.0;        // P_msm_shikaku_i: the quad mesh is the texture's 16 px
uniform sampler2D ramp : repeat_disable;   // the prm colour + alpha over the life (GradientTexture1D)

void start() {
    float a = float(INDEX) * TAU / count;                 // FixedInterval: angle = (i << 16) / 14
    vec2 dir = vec2(sin(a), cos(a));                      // the circle's point (0, 0, r) turned by Y: (r sin, 0, r cos) -> the screen's XY after Rx(90)
    vec2 pos = vec2(rim_radius * rim_scale_x * dir.x, rim_radius * dir.y);
    VELOCITY = vec3(dir * speed, 0.0);
    CUSTOM = vec4(0.0);                                   // .y = the life fraction
    TRANSFORM = mat4(1.0);
    TRANSFORM[3].xy = pos;
    ACTIVE = true;
}

void process() {
    if (ACTIVE) {
        CUSTOM.y += DELTA / LIFETIME;
        if (CUSTOM.y >= 1.0) {
            ACTIVE = false;
        } else {
            TRANSFORM[3].xy += VELOCITY.xy * DELTA;            // the frame's move, then the drag (JPAParticle: pos += vel; vel *= airResist)
            VELOCITY.xy *= pow(drag, DELTA * 60.0);
            vec2 dir = length(VELOCITY.xy) > 1e-4 ? normalize(VELOCITY.xy) : vec2(0.0, 1.0);
            TRANSFORM[0].xyz = vec3(dir.y, -dir.x, 0.0) * (quad_across / tex_size);   // across the flight
            TRANSFORM[1].xyz = vec3(dir, 0.0) * (quad_along / tex_size);              // along it (DirBillboard's Y)
            COLOR = texture(ramp, vec2(CUSTOM.y, 0.5));
        }
    }
}
