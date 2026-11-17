## probe_clip.gd -- what does a clip DO over time? For a scene with an AnimationPlayer and a Skeleton3D, sample a clip every N frames and print, per
## named bone, its global (skeleton-space) height and how upright it is (its up axis' y: 1 upright, -1 upside down) and its forward's yaw.
## Usage: godot --headless -s tools/anim/probe_clip.gd -- <scene.tscn> <clip> [every_frames] [bone ...]   (no bones = the roots and the biggest movers)
extends SceneTree

func _initialize() -> void:
	var code := _run()
	print("[PROBE] %s" % ("OK" if code == 0 else "FAILED (%d)" % code))
	quit(code)

func _find(n: Node, type: String) -> Node:
	if n.get_class() == type: return n
	for c in n.get_children():
		var f := _find(c, type)
		if f != null: return f
	return null

func _run() -> int:
	var args := OS.get_cmdline_user_args()
	if args.size() < 2: push_error("usage: -- <scene> <clip> [every] [bones...]"); return 2
	var packed := load(args[0]) as PackedScene
	if packed == null: push_error("no scene " + args[0]); return 3
	var root := packed.instantiate()
	var ap := _find(root, "AnimationPlayer") as AnimationPlayer
	var skel := _find(root, "Skeleton3D") as Skeleton3D
	if ap == null or skel == null: push_error("no AnimationPlayer / Skeleton3D"); root.free(); return 4
	var clip := args[1]
	if not ap.has_animation(clip):
		print("[PROBE] clips: ", ap.get_animation_list()); push_error("no clip " + clip); root.free(); return 5
	var anim := ap.get_animation(clip)
	var every := int(args[2]) if args.size() > 2 else 10
	var fps := 30.0
	var frames := int(round(anim.length * fps))
	var names := []
	for b in skel.get_bone_count(): names.append(skel.get_bone_name(b))
	print("[PROBE] %s: %.2f s (%d frames at 30), loop %d, %d tracks; bones: %s" % [clip, anim.length, frames, anim.loop_mode, anim.get_track_count(), ", ".join(names)])
	var bones := []
	if args.size() > 3:
		for i in range(3, args.size()):
			var b := skel.find_bone(args[i])
			if b < 0: push_error("no bone " + args[i]); root.free(); return 6
			bones.append(b)
	else:
		for b in skel.get_parentless_bones(): bones.append(b)
		for b in skel.get_bone_count(): if bones.size() < 4 and not bones.has(b): bones.append(b)
	# the base: the rest pose
	var n := skel.get_bone_count()
	var rest_local := []; rest_local.resize(n)
	for b in n: rest_local[b] = skel.get_bone_rest(b)
	var f := 0
	while f <= frames:
		var t := f / fps
		var local := rest_local.duplicate()
		for ti in anim.get_track_count():
			var b := skel.find_bone(String(anim.track_get_path(ti).get_concatenated_subnames()))
			if b < 0: continue
			var tr: Transform3D = local[b]
			match anim.track_get_type(ti):
				Animation.TYPE_ROTATION_3D: tr = Transform3D(Basis(anim.rotation_track_interpolate(ti, t)).scaled(tr.basis.get_scale()), tr.origin)
				Animation.TYPE_POSITION_3D: tr = Transform3D(tr.basis, anim.position_track_interpolate(ti, t))
				Animation.TYPE_SCALE_3D: tr = Transform3D(Basis(tr.basis.get_rotation_quaternion()).scaled(anim.scale_track_interpolate(ti, t)), tr.origin)
			local[b] = tr
		var g := []; g.resize(n)
		for r in skel.get_parentless_bones(): _fk(skel, r, Transform3D.IDENTITY, local, g)
		var line := "[PROBE] f%3d t%.2f" % [f, t]
		for b in bones:
			var gb: Transform3D = g[b]; var bs := gb.basis.orthonormalized()
			line += " | %s y %.1f up.y %+.2f fwd.y %+.2f yaw %+.0f" % [names[b], gb.origin.y, bs.y.y, bs.z.y, rad_to_deg(atan2(bs.z.x, bs.z.z))]
		print(line)
		f += every
	root.free()
	return 0

func _fk(skel: Skeleton3D, b: int, parent_g: Transform3D, local: Array, out: Array) -> void:
	var gg: Transform3D = parent_g * local[b]
	out[b] = gg
	for c in skel.get_bone_children(b): _fk(skel, c, parent_g * local[b], local, out)
