## bake_anim.gd -- SMS 176: bake a POSE SCRIPT (JSON) into a real Godot Animation (.tres) on Mario's rig, so a custom clip can be made
## from a few hand-written key poses and then edited like any other clip in the editor's Animation panel.
##
## Run headless from the project root (Windows):
##   godot --headless -s tools/anim/bake_anim.gd -- res://assets/Mario/Anims/ma_lookabout.pose.json res://assets/Mario/Anims/ma_lookabout.tres
## Optional 3rd / 4th args: the Mario scene (default res://assets/Mario.tscn) and the base clip (default the script's base_clip, else ma_wait).
##
## The pose script: {"name", "length" (s), "fps", "loop", "base_clip", "keys": [{"t": seconds, "pose": {joint: [[axis, degrees], ...]}}]}.
## Each key poses joints by rotations about the BODY's axes in the skeleton's space -- "up" (+Y: positive = turn to his LEFT), "left" (+X:
## positive = tip forward / nod down, negative = look UP), "forward" (+Z: positive = roll toward his left shoulder) -- applied in order AT the
## joint, its children following (the parent's turn carries the child, then the child's own). Joints not named hold the base clip's pose at
## base_frame (default 0). Posed joints get a rotation key at every key time (cubic); every other bone gets one key at 0 so the clip is whole.
## The baked clip is a normal Animation: edit, re-time or add keys in the editor, or re-bake from the script (the bake OVERWRITES the .tres).
extends SceneTree

const AXES := {"up": Vector3(0, 1, 0), "left": Vector3(1, 0, 0), "forward": Vector3(0, 0, 1)}
## translations along the body's axes (a lowered centre for a crouch), in the rig's units (Mario stands 117 of them: the game's)
const MOVES := {"move_up": Vector3(0, 1, 0), "move_left": Vector3(1, 0, 0), "move_forward": Vector3(0, 0, 1)}

func _initialize() -> void:
	var code := 1
	# everything inside one guarded call so a mistake still ends with quit() (a dangling headless tree never exits)
	code = _run()
	print("[BAKE] %s" % ("OK" if code == 0 else "FAILED (%d)" % code))
	quit(code)

func _run() -> int:
	var args := OS.get_cmdline_user_args()
	if args.size() < 2:
		push_error("usage: -- <pose.json> <out.tres> [mario.tscn] [base_clip]"); return 2
	var script_path: String = args[0]; var out_path: String = args[1]
	var scene_path: String = args[2] if args.size() > 2 else "res://assets/Mario.tscn"
	var text := FileAccess.get_file_as_string(script_path)
	if text == "":
		push_error("cannot read " + script_path); return 3
	var script = JSON.parse_string(text)
	if typeof(script) != TYPE_DICTIONARY:
		push_error("not a JSON object: " + script_path); return 3
	var base_clip: String = args[3] if args.size() > 3 else String(script.get("base_clip", "ma_wait"))
	var base_frame: int = int(script.get("base_frame", 0))
	var fps: float = float(script.get("fps", 30))
	var length: float = float(script.get("length", 0.0))
	var loop: bool = bool(script.get("loop", false))

	var packed := load(scene_path) as PackedScene
	if packed == null:
		push_error("cannot load " + scene_path); return 4
	var mario := packed.instantiate()
	var ap := mario.get_node_or_null("AnimationPlayer") as AnimationPlayer
	var skel := mario.get_node_or_null("Armature/Skeleton3D") as Skeleton3D
	if ap == null or skel == null:
		push_error("the scene has no AnimationPlayer / Armature/Skeleton3D"); mario.free(); return 5
	if not ap.has_animation(base_clip):
		push_error("no base clip " + base_clip + " in the player (" + str(ap.get_animation_list().size()) + " clips)"); mario.free(); return 6
	var base: Animation = ap.get_animation(base_clip)
	var t0 := base_frame / fps

	# the base pose per bone, and the track path each bone's tracks use
	var n := skel.get_bone_count()
	var pos := []; var rot := []; var scl := []; var path_of := []; var has_pos := []; var has_scl := []
	pos.resize(n); rot.resize(n); scl.resize(n); path_of.resize(n); has_pos.resize(n); has_scl.resize(n)
	var node_path := ""
	for b in n:
		var r := skel.get_bone_rest(b)
		pos[b] = r.origin; rot[b] = r.basis.get_rotation_quaternion(); scl[b] = r.basis.get_scale(); path_of[b] = ""; has_pos[b] = false; has_scl[b] = false
	for ti in base.get_track_count():
		var p := base.track_get_path(ti)
		var bone_name := String(p.get_concatenated_subnames())
		var b := skel.find_bone(bone_name)
		if b < 0: continue
		path_of[b] = String(p); node_path = String(p.get_concatenated_names())
		match base.track_get_type(ti):
			Animation.TYPE_POSITION_3D: pos[b] = base.position_track_interpolate(ti, t0); has_pos[b] = true
			Animation.TYPE_ROTATION_3D: rot[b] = base.rotation_track_interpolate(ti, t0)
			Animation.TYPE_SCALE_3D: scl[b] = base.scale_track_interpolate(ti, t0); has_scl[b] = true
	for b in n:
		if path_of[b] == "": path_of[b] = node_path + ":" + skel.get_bone_name(b)   # a bone the base clip never keys: its rest, on the same skeleton path
	var base_local := []; base_local.resize(n)
	for b in n: base_local[b] = Transform3D(Basis(rot[b]).scaled(scl[b]), pos[b])

	# the keys
	var keys: Array = script.get("keys", [])
	keys.sort_custom(func(a, c): return float(a["t"]) < float(c["t"]))
	if keys.is_empty():
		push_error("no keys"); mario.free(); return 7
	if length <= 0.0: length = float(keys[-1]["t"])
	var posed := {}   # bone -> true for bones any key turns
	var moved := {}   # bone -> true for bones any key moves
	var key_locals := []   # per key: Array of local transforms per bone
	for k in keys:
		var deltas := {}; var moves := {}
		var pose: Dictionary = k.get("pose", {})
		for joint in pose.keys():
			var b := skel.find_bone(joint)
			if b < 0:
				push_error("no bone " + joint); mario.free(); return 8
			var R := Basis(); var mv := Vector3.ZERO
			for pair in pose[joint]:
				if AXES.has(pair[0]): R = Basis(AXES[pair[0]], deg_to_rad(float(pair[1]))) * R
				elif MOVES.has(pair[0]): mv += MOVES[pair[0]] * float(pair[1])
				else:
					push_error("unknown axis " + str(pair[0]) + " on " + joint); mario.free(); return 8
			deltas[b] = R; posed[b] = true
			if mv != Vector3.ZERO: moves[b] = mv; moved[b] = true
		var locals := []; locals.resize(n)
		for root in skel.get_parentless_bones():
			_pose_chain(skel, root, Transform3D.IDENTITY, base_local, deltas, moves, locals)
		key_locals.append(locals)

	# the animation
	var anim := Animation.new()
	anim.length = length
	anim.loop_mode = Animation.LOOP_LINEAR if loop else Animation.LOOP_NONE
	anim.step = 1.0 / fps
	var rot_keys := 0
	for b in n:
		var tr := anim.add_track(Animation.TYPE_ROTATION_3D)
		anim.track_set_path(tr, NodePath(path_of[b]))
		anim.track_set_interpolation_type(tr, Animation.INTERPOLATION_CUBIC)
		if posed.has(b):
			for ki in keys.size():
				anim.rotation_track_insert_key(tr, float(keys[ki]["t"]), key_locals[ki][b].basis.get_rotation_quaternion()); rot_keys += 1
		else:
			anim.rotation_track_insert_key(tr, 0.0, rot[b]); rot_keys += 1
		if moved.has(b):
			var tm := anim.add_track(Animation.TYPE_POSITION_3D); anim.track_set_path(tm, NodePath(path_of[b])); anim.track_set_interpolation_type(tm, Animation.INTERPOLATION_CUBIC)
			for ki in keys.size(): anim.position_track_insert_key(tm, float(keys[ki]["t"]), key_locals[ki][b].origin)
		elif has_pos[b]:
			var tp := anim.add_track(Animation.TYPE_POSITION_3D); anim.track_set_path(tp, NodePath(path_of[b])); anim.position_track_insert_key(tp, 0.0, pos[b])
		if has_scl[b]:
			var ts := anim.add_track(Animation.TYPE_SCALE_3D); anim.track_set_path(ts, NodePath(path_of[b])); anim.scale_track_insert_key(ts, 0.0, scl[b])
	anim.resource_name = String(script.get("name", out_path.get_file().get_basename()))
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(out_path.get_base_dir()))
	var err := ResourceSaver.save(anim, out_path)
	if err != OK:
		push_error("save failed: " + str(err)); mario.free(); return 9
	var centre := skel.find_bone("center")
	print("[BAKE] %s: %d bones, %d rotation keys over %d key poses (%d bones moved), %.2f s at %d fps, %s, base %s@%d -> %s; the centre bone at y %.2f in the base pose (the rig is in the game's units: he stands 117)" % [anim.resource_name, n, rot_keys, keys.size(), moved.size(), length, int(fps), "a loop" if loop else "one-shot", base_clip, base_frame, out_path, pos[centre].y if centre >= 0 else -1.0])

	# a read-back: the head's forward (yaw from the body's forward, pitch) at each key, from the saved clip's own tracks
	var saved := ResourceLoader.load(out_path, "Animation", ResourceLoader.CACHE_MODE_IGNORE) as Animation   # the scene may hold the OLD clip at this path in the cache: read the file
	var head := skel.find_bone("jnt_head")
	var base_fk := _fk_at(null, skel, 0.0, base_local)   # the base pose itself
	var head_fwd0: Vector3 = base_fk[head].basis.inverse() * Vector3(0, 0, 1)
	for ki in keys.size():
		var t := float(keys[ki]["t"])
		var fk := _fk_at(saved, skel, t, base_local)
		var fwd: Vector3 = (fk[head].basis * head_fwd0).normalized()
		var yaw := rad_to_deg(atan2(fwd.x, fwd.z)); var pitch := rad_to_deg(asin(clamp(fwd.y, -1.0, 1.0)))
		var hl := skel.find_bone("jnt_hand_L"); var hr := skel.find_bone("jnt_hand_R"); var fl := skel.find_bone("jnt_foot_L"); var fr := skel.find_bone("jnt_foot_R")
		print("[BAKE] t=%.2f head yaw %+.0f (+ = his left) pitch %+.0f (+ = up); hands y L %.1f R %.1f z L %.1f R %.1f; feet y L %.1f R %.1f z L %.1f R %.1f; centre y %.1f" % [t, yaw, pitch, fk[hl].origin.y, fk[hr].origin.y, fk[hl].origin.z, fk[hr].origin.z, fk[fl].origin.y, fk[fr].origin.y, fk[fl].origin.z, fk[fr].origin.z, fk[centre].origin.y])
	mario.free()
	return 0

## The key's pose down a chain: the bone under its posed parent, its own body-axis turn applied at its origin and its move added; the children under that.
func _pose_chain(skel: Skeleton3D, b: int, parent_g: Transform3D, base_local: Array, deltas: Dictionary, moves: Dictionary, out: Array) -> void:
	var local: Transform3D = base_local[b]
	var g := parent_g * local
	if deltas.has(b) or moves.has(b):
		var basis: Basis = deltas[b] * g.basis if deltas.has(b) else g.basis
		var origin: Vector3 = g.origin + (moves[b] if moves.has(b) else Vector3.ZERO)
		g = Transform3D(basis, origin)
		local = parent_g.affine_inverse() * g
	out[b] = local
	for c in skel.get_bone_children(b):
		_pose_chain(skel, c, g, base_local, deltas, moves, out)

## Forward kinematics of a saved clip at a time (null = the base pose): the global (skeleton-space) transform per bone, the rotation and position
## tracks sampled, scales the base's.
func _fk_at(anim: Animation, skel: Skeleton3D, t: float, base_local: Array) -> Array:
	var n := skel.get_bone_count()
	var local := []; local.resize(n)
	for b in n: local[b] = base_local[b]
	if anim != null:
		for ti in anim.get_track_count():
			var b := skel.find_bone(String(anim.track_get_path(ti).get_concatenated_subnames()))
			if b < 0: continue
			if anim.track_get_type(ti) == Animation.TYPE_ROTATION_3D:
				var q: Quaternion = anim.rotation_track_interpolate(ti, t)
				local[b] = Transform3D(Basis(q).scaled(base_local[b].basis.get_scale()), local[b].origin)
			elif anim.track_get_type(ti) == Animation.TYPE_POSITION_3D:
				local[b] = Transform3D(local[b].basis, anim.position_track_interpolate(ti, t))
	var g := []; g.resize(n)
	for root in skel.get_parentless_bones():
		_fk_chain(skel, root, Transform3D.IDENTITY, local, g)
	return g

func _fk_chain(skel: Skeleton3D, b: int, parent_g: Transform3D, local: Array, out: Array) -> void:
	var gg: Transform3D = parent_g * local[b]
	out[b] = gg
	for c in skel.get_bone_children(b):
		_fk_chain(skel, c, gg, local, out)
