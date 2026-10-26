@tool
extends EditorScenePostImport
# The launcher's skeleton (generator_model1.bmd): the import loads the bones' REST from the wait clip (rest_pose/load_pose), but leaves the bone
# POSE at the glb's bind -- and the editor draws the pose. The clips turn the root +90 deg about X and Launcher.tscn's Model carries the game's
# -90 (mRotation.x + 270), so at the bind pose the pile stood on its edge in the editor while it lay flat in play. Pose := rest: the editor
# shows the wait clip's first frame, the same pile the game shows.
func _post_import(scene: Node) -> Object:
	for skel in scene.find_children("*", "Skeleton3D", true, false):
		skel.reset_bone_poses()
	return scene
