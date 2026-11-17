// The screen fader -- TSMSFader (GC2D/ScrnFader.cpp) with the Hx wiper (GC2D/hx_wiper.c) on a full-screen rect.
// mode 0 clear; 1 solid (FADE_STATUS_FULLY_FADED_OUT: fill_rect); 2 plain fade (alpha); 3 the death wipe Hx_Test2: four black bands,
// each the part of a ring about a centre off the right edge (cx = width + 200 / 250) revealed row by row -- bands 1 and 3 from the
// bottom up, 2 and 4 from the top down -- that fill the screen from the left as curved stripes; 4 the circle iris Hx_Circle closing
// on the centre: black outside the radius with a HARD edge (Hxs1_Circle: each row's black drawn to the circle's edge) and, inside it, three
// faint black rings 20 px wide (Hxs2_Circle: iris_alpha, darkening by 1.5 / 0.75 / 0.5 of 255 a frame -- at most 15 / 7 / 5 % at the end;
// iris_drift, each ring's inner edge creeping out 0.05 / 0.12 / 0.25 px a frame) -- 214: 61's soft 60-px rim (a smoothstep) was the port's; 5 the gate's spiral Hx_Test4 (type 8, SMS pass 127): a black band 2 x thin wide winding in from
// radius 520 about the centre, 2.4 px of radius per 0.12 rad, five more segments a frame for 38 frames (rstep 0 -> 190, thin 124.3 +
// 0.15 a frame) -- a turn drops 126 px and the band is 249 wide, so the turns overlap and the hole shrinks with a spiral edge sweeping
// round about five times (radius 520 = the half width + 200). Geometry in the wiper's 640 x 448 frame -- the game's render height (Resolution.cpp 63,
// TSMSFader::setDisplaySize), where the four rings leave no hole; at 480 rows the bottom 30 showed a wedge between the first and
// third bands (pass 61) -- stretched to the viewport.
shader_type canvas_item;

uniform int mode = 0;
uniform float alpha = 1.0;
uniform vec4 rows = vec4(0.0);      // Hx_Test2: rows revealed per band (frame px), 600 = all
uniform float radius = 400.0;      // Hx_Circle: the iris radius (frame px)
uniform vec3 iris_alpha = vec3(0.0);   // Hx_Circle: the three inner rings' darkness (a1 / a2 / a3 >> 8, as 0..1)
uniform vec3 iris_drift = vec3(0.0);   // Hx_Circle: p1 / p2 / p3, each ring's inner edge's creep (frame px)
uniform vec2 viewport = vec2(640.0, 480.0);
uniform vec3 color = vec3(0.0);        // TSMSFader::setColor: black, or the white the select screen fades through (pass 127)
uniform vec2 spiral = vec2(0.0, 124.3);   // Hx_Test4: (rstep segments drawn, thin = the band's half width), frame px

bool band(vec2 p, vec2 c, float inner, float outer, float revealed, bool bottom_up) {
	if (revealed <= 0.0) {
		return false;
	}
	bool in_rows = bottom_up ? (p.y >= 448.0 - revealed) : (p.y <= revealed);
	if (!in_rows) {
		return false;
	}
	float d = distance(p, c);
	return d <= outer && d >= inner;
}

void fragment() {
	vec2 px = SCREEN_UV * viewport;
	vec2 p = vec2(px.x / viewport.x * 640.0, px.y / viewport.y * 448.0);
	float a = 0.0;
	if (mode == 1) {
		a = 1.0;
	} else if (mode == 2) {
		a = alpha;
	} else if (mode == 3) {
		bool dark = band(p, vec2(840.0, 300.0), 650.0, 900.0, rows.x, true)
			|| band(p, vec2(840.0, 150.0), 450.0, 700.0, rows.y, false)
			|| band(p, vec2(890.0, 370.0), 400.0, 650.0, rows.z, true)
			|| band(p, vec2(890.0, 300.0), 200.0, 420.0, rows.w, false);
		a = dark ? 1.0 : 0.0;
	} else if (mode == 4) {
		// Hx_Circle: the hard black outside the radius; the rings from the edge in: (r - 20 + p1 .. r), (r - 40 + p2 .. r - 20 + p1), (r - 60 + p3 .. r - 40 + p2),
		// each drawn only once the radius is past 22 / 42 / 62
		float d = distance(p, vec2(320.0, 224.0));
		if (d >= radius) {
			a = 1.0;
		} else if (radius > 22.0 && d >= radius - 20.0 + iris_drift.x) {
			a = iris_alpha.x;
		} else if (radius > 42.0 && d >= radius - 40.0 + iris_drift.y) {
			a = iris_alpha.y;
		} else if (radius > 62.0 && d >= radius - 60.0 + iris_drift.z) {
			a = iris_alpha.z;
		}
	} else if (mode == 5) {
		// the spiral: the game's angle a has x = r sin a, y = r cos a (y down), from 0 at the bottom growing counter-clockwise on screen
		vec2 q = p - vec2(320.0, 224.0);
		float d = length(q);
		float phi = atan(q.x, q.y);
		if (phi < 0.0) {
			phi += TAU;
		}
		float theta_max = spiral.x * 0.12;
		float k = floor((theta_max - phi) / TAU);   // the latest turn that has swept this angle
		if (k >= 0.0) {
			float theta = phi + TAU * k;
			float r = 520.0 - 20.0 * theta;          // 2.4 px per 0.12 rad
			float inner = max(r - spiral.y, 0.0);
			a = d >= inner ? 1.0 : 0.0;
		}
	}
	COLOR = vec4(color, a);
}
