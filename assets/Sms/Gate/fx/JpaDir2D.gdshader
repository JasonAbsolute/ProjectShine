// JpaDir2D.gdshader -- a 2D JPA streak for GPUParticles2D (SMS pass 127h). The game's DirBillboard / Direction / Line shapes with pivot Y 2
// (JPADraw.cpp 813: unkC.y = unk4.y x (pivotY - 1)) are NOT centred on the particle: the quad spans 0 .. 2h along the particle's velocity
// (JPADrawVisitor.cpp JPADrawExecDirBillBoard 928-984: tex v = 0, the senko's soft point, at the far end; v = 1, its bright flat base, AT the
// particle), and a Line (JPADrawExecLine 1096-1119) runs from the particle BACK along the velocity. Godot draws a particle's texture centred
// on it, so this shifts the quad along the particle's own Y (= the velocity with particle_flag_align_y; +Y is the flight direction) by half
// its height before the particle transform, and flips v so the anchored end is the texture's base.
//   anchor   +1: the base at the particle, the streak growing AHEAD along the velocity (DirBillboard / Direction, pivot Y 2)
//            -1: the far end at the particle, the streak trailing BEHIND (a GX line drawn back along the velocity)
//             0: centred (a plain billboard)
//   flip_v   true for the senko textures (their bright base is the BOTTOM row, v = 1): with anchor +1 the base must sit at the particle end
// Additive (SRCALPHA / ONE), colour = TEXC x prm: the particle's COLOR carries the colour table (the ramp by age), the texture its own I / A.
shader_type canvas_item;
render_mode blend_add;

uniform float anchor = 1.0;
uniform bool flip_v = true;

void vertex() {
	// VERTEX here is the texture-sized quad about the particle, BEFORE the particle's transform (canvas.glsl scales the unit quad by the
	// texture size for particles and multiplies the instance transform in after the user code), so a shift along +Y is along the flight.
	float half_h = 0.5 / TEXTURE_PIXEL_SIZE.y;
	VERTEX.y += anchor * half_h;
}

void fragment() {
	vec2 uv = UV;
	if (flip_v) {
		uv.y = 1.0 - uv.y;
	}
	COLOR = texture(TEXTURE, uv) * COLOR;
}
