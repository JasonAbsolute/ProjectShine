// Additive, camera-facing "shooting streaks" for the Shine Sprite. Fully
// procedural: N thin streaks at fixed random angles, each firing on its OWN
// random cycle so light shoots out in random 360-degree directions with slight
// delays. Each streak has a CONSTANT perpendicular width (so it stays line-thin
// instead of fanning into a cone), tapers toward the tip, and fades softly at
// the end (no hard cut-off). A soft central core keeps the origin glowing.
// Billboarded in the vertex stage so it always faces the camera.
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

uniform vec4 tint : source_color = vec4(1.0, 0.85, 0.4, 1.0);
uniform float intensity = 1.0;
uniform float speed = 1.0;       // global rate multiplier (how OFTEN rays fire)
uniform float ray_life = 0.35;   // fraction of each cycle a ray is visible (smaller = shorter/snappier)
uniform float beam_width = 0.022; // perpendicular half-width (smaller = thinner)
uniform float core = 0.4;        // steady central glow amount

const int RAYS = 14;

float hash(float n) { return fract(sin(n * 12.9898) * 43758.5453); }

void vertex() {
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
		INV_VIEW_MATRIX[0] * length(MODEL_MATRIX[0].xyz),
		INV_VIEW_MATRIX[1] * length(MODEL_MATRIX[1].xyz),
		INV_VIEW_MATRIX[2] * length(MODEL_MATRIX[2].xyz),
		MODEL_MATRIX[3]
	);
}

void fragment() {
	vec2 p = (UV - vec2(0.5)) * 2.0; // -1..1
	float rr = length(p);

	float acc = 0.0;
	for (int i = 0; i < RAYS; i++) {
		float fi = float(i);
		float a = hash(fi) * 6.28318;               // fixed random direction
		vec2 dir = vec2(cos(a), sin(a));
		float along = dot(p, dir);                  // distance out along the streak
		if (along <= 0.0) {
			continue;                               // forward side only
		}
		float perp = length(p - along * dir);       // perpendicular distance to the line

		float rate = (0.18 + hash(fi + 3.7) * 0.4) * speed; // random shots/sec
		float ph = hash(fi + 9.1);                  // random phase (staggers shots)
		float ct = fract(TIME * rate + ph);         // 0..1 through the whole cycle
		float cw = ct / ray_life;                   // 0..1 through the SHORT active window
		if (cw > 1.0) {
			continue;                               // ray is dark for the rest of its cycle
		}
		float reach = 1.25 * smoothstep(0.0, 0.4, cw); // tip extends outward

		// Soft-edged ray = a thin bright CORE wrapped in a wide dim GLOW, so it
		// reads as translucent light rather than a solid line. Width tapers toward
		// the tip.
		float w = beam_width * (1.0 - 0.4 * clamp(along / max(reach, 0.001), 0.0, 1.0));
		float coreL = exp(-(perp * perp) / (2.0 * w * w));
		float glowL = exp(-(perp * perp) / (2.0 * (w * 3.2) * (w * 3.2)));
		float profile = coreL * 0.55 + glowL * 0.5;

		// Airy length shaping: brightest near the source, long soft fade to the
		// tip (no hard cut), quick fade-in right at the center.
		float t = clamp(along / max(reach, 0.001), 0.0, 1.0);
		float lenFall = mix(1.0, 0.25, t);          // dimmer the further out
		float tipFade = smoothstep(reach, reach * 0.55, along); // soft tip
		float baseIn = smoothstep(0.0, 0.06, along);
		// Start STRONG (quick attack so it doesn't hard-pop in), then fade away
		// over the rest of the streak's life.
		float life = smoothstep(0.0, 0.06, cw) * (1.0 - smoothstep(0.06, 1.0, cw));

		acc += profile * lenFall * tipFade * baseIn * life;
	}

	acc += core * exp(-rr * rr * 26.0); // soft central glow

	ALBEDO = tint.rgb;
	// Soft, translucent falloff — no hard saturation to a solid edge.
	ALPHA = 1.0 - exp(-acc * intensity);
}
