# menudrive255.gd -- does a controller drive the Player Select and the Level Select? (SMS 255) A headless SceneTree runner: loads CharacterSelect.tscn,
# then feeds it (1) a plain joypad on device 0 (what an Xbox pad / Delfinovin gives Godot: InputEventJoypadMotion axis 0, InputEventJoypadButton 0 / 1)
# and (2) the GameCube adapter's own reports (SmsGcAdapter.InjectReport -> joypad events on device 20), and watches the GUI focus owner (the poster
# under the hand), the scene change on A and the way back on B. Every step prints PASS or FAIL; the log tags [M255].
extends SceneTree

var _fails := 0
var _gc: Node

func _initialize() -> void:
	var err := change_scene_to_file("res://assets/CharacterSelect.tscn")
	print("[M255] load CharacterSelect -> ", err)
	call_deferred("_run")

func _check(ok: bool, what: String) -> void:
	if not ok: _fails += 1
	print("[M255] ", "PASS" if ok else "FAIL", ": ", what)

func _focus_name() -> String:
	var f := root.gui_get_focus_owner()
	return f.name if f else "<none>"

func _frames(n: int) -> void:
	for i in n:
		await process_frame

# a plain pad: the left stick along X for two frames, then back to centre
func _pad_stick(device: int, x: float) -> void:
	var ev := InputEventJoypadMotion.new(); ev.device = device; ev.axis = JOY_AXIS_LEFT_X; ev.axis_value = x
	Input.parse_input_event(ev)
	await _frames(3)
	var back := InputEventJoypadMotion.new(); back.device = device; back.axis = JOY_AXIS_LEFT_X; back.axis_value = 0.0
	Input.parse_input_event(back)
	await _frames(3)

func _pad_button(device: int, button: int) -> void:
	var ev := InputEventJoypadButton.new(); ev.device = device; ev.button_index = button; ev.pressed = true
	Input.parse_input_event(ev)
	await _frames(2)
	var up := InputEventJoypadButton.new(); up.device = device; up.button_index = button; up.pressed = false
	Input.parse_input_event(up)
	await _frames(2)

# the adapter's 37-byte report: 0x21, then port 1 (status, buttons 1: A B X Y Dl Dr Dd Du; buttons 2: Start Z R L; sticks; triggers), ports 2-4 empty
func _report(status: int, b1: int, b2: int, sx: int, sy: int, cx: int, cy: int, tl: int, tr: int) -> PackedByteArray:
	var r := PackedByteArray(); r.resize(37); r.fill(0)
	r[0] = 0x21
	r[1] = status; r[2] = b1; r[3] = b2; r[4] = sx; r[5] = sy; r[6] = cx; r[7] = cy; r[8] = tl; r[9] = tr
	return r

# the adapter pumps its reports into Godot in _PhysicsProcess: wait on PHYSICS frames (headless, process frames outrun them by far)
func _pframes(n: int) -> void:
	for i in n:
		await physics_frame
	await process_frame

func _gc_rest() -> void:
	_gc.call("InjectReport", _report(0x10, 0, 0, 128, 128, 128, 128, 30, 30))
	await _pframes(3)

func _gc_stick(sx: int) -> void:
	_gc.call("InjectReport", _report(0x10, 0, 0, sx, 128, 128, 128, 30, 30))
	await _pframes(3)
	print("[M255]   adapter stick ", sx, ": device 20 axis 0 = ", Input.get_joy_axis(20, JOY_AXIS_LEFT_X), ", ui_right pressed ", Input.is_action_pressed("ui_right"), ", Lstick_right ", Input.get_action_strength("Lstick_right"))
	await _gc_rest()

func _gc_button(b1: int) -> void:
	_gc.call("InjectReport", _report(0x10, b1, 0, 128, 128, 128, 128, 30, 30))
	await _pframes(3)
	print("[M255]   adapter buttons ", b1, ": button_a ", Input.is_action_pressed("button_a"), " button_b ", Input.is_action_pressed("button_b"), " ui_accept ", Input.is_action_pressed("ui_accept"), " ui_cancel ", Input.is_action_pressed("ui_cancel"))
	await _gc_rest()

func _wait_scene(name: String, limit_s: float) -> bool:
	var t0 := Time.get_ticks_msec()
	while Time.get_ticks_msec() - t0 < limit_s * 1000.0:
		await process_frame
		if current_scene != null and current_scene.name == name: return true
	return false

func _run() -> void:
	await _frames(20)
	var cs := current_scene
	_check(cs != null and cs.name == "CharacterSelect", "the Player Select loaded (" + str(cs.name if cs else null) + ")")
	_gc = root.get_node_or_null("SmsGcAdapter")
	_check(_gc != null, "the SmsGcAdapter autoload is there")
	print("[M255] ui_right events: ", InputMap.action_get_events("ui_right"))
	print("[M255] ui_accept events: ", InputMap.action_get_events("ui_accept"))
	print("[M255] ui_cancel events: ", InputMap.action_get_events("ui_cancel"))
	print("[M255] focus at start: ", _focus_name())
	var f0 := _focus_name()
	# (1) a plain pad on device 0
	await _pad_stick(0, 1.0)
	var f1 := _focus_name()
	print("[M255] after pad stick right: focus ", f1)
	_check(f1 != f0 and f1 != "<none>", "a plain pad's stick right moves the hand (" + f0 + " -> " + f1 + ")")
	await _pad_stick(0, -1.0)
	var f2 := _focus_name()
	_check(f2 == f0, "and stick left brings it back (" + f1 + " -> " + f2 + ")")
	# (2) the adapter: stick right, then rest
	if _gc != null:
		await _gc_rest()
		await _gc_stick(208)
		var f3 := _focus_name()
		print("[M255] after the adapter's stick right: focus ", f3, "; adapter device 20 axis 0 = ", Input.get_joy_axis(20, JOY_AXIS_LEFT_X))
		_check(f3 != f2 and f3 != "<none>", "the adapter's stick right moves the hand (" + f2 + " -> " + f3 + ")")
		await _gc_stick(48)
		_check(_focus_name() == f2, "and its stick left brings it back (" + _focus_name() + ")")
	# A on the plain pad confirms the highlighted character -> the Level Select
	await _pad_button(0, JOY_BUTTON_A)
	var got := await _wait_scene("LevelSelect", 6.0)
	_check(got, "a plain pad's A confirms the character and the Level Select comes up (scene now " + str(current_scene.name if current_scene else null) + ")")
	if not got:
		print("[M255] DONE fails=", _fails); quit(1); return
	await _frames(40)
	print("[M255] Level Select focus at start: ", _focus_name())
	var l0 := _focus_name()
	await _pad_stick(0, 1.0)
	var l1 := _focus_name()
	_check(l1 != l0 and l1 != "<none>", "Level Select: a plain pad's stick right moves the hand (" + l0 + " -> " + l1 + ")")
	if _gc != null:
		await _gc_stick(208)
		var l2 := _focus_name()
		_check(l2 != l1 and l2 != "<none>", "Level Select: the adapter's stick right moves the hand (" + l1 + " -> " + l2 + ")")
		# the adapter's B goes back to the Player Select (bit 1 of buttons 1 = B)
		await _gc_button(0x02)
		var back := await _wait_scene("CharacterSelect", 6.0)
		_check(back, "the adapter's B goes back to the Player Select (scene now " + str(current_scene.name if current_scene else null) + ")")
		if back:
			await _frames(30)
			# and the adapter's A picks the character (bit 0 = A)
			await _gc_button(0x01)
			var again := await _wait_scene("LevelSelect", 6.0)
			_check(again, "the adapter's A confirms the character (scene now " + str(current_scene.name if current_scene else null) + ")")
	print("[M255] DONE fails=", _fails)
	await process_frame
	quit(0)
