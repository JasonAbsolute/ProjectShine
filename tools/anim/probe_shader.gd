## probe_shader.gd -- the uniforms a shader exposes (includes expanded): does the crown shader know crown_recolor?
## Usage: godot --headless -s tools/anim/probe_shader.gd -- res://path/to.gdshader [uniform_name]
extends SceneTree

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	var sh := load(args[0]) as Shader
	if sh == null:
		print("[PROBE] no shader ", args[0]); quit(2); return
	var names := []
	for u in sh.get_shader_uniform_list():
		names.append(String(u["name"]))
	print("[PROBE] %s: %d uniforms: %s" % [args[0], names.size(), ", ".join(names)])
	if args.size() > 1:
		print("[PROBE] has %s: %s" % [args[1], str(names.has(args[1]))])
	var code: String = sh.code
	print("[PROBE] code %d chars; mentions crown_recolor in its own text: %s" % [code.length(), str(code.contains("crown_recolor"))])
	quit(0)
