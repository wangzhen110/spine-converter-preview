extends SceneTree

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 2:
		_fail("usage: -- <skeleton.json|skel> <atlas>")
		return
	var skeleton_path: String = args[0]
	var atlas_path: String = args[1]
	if not FileAccess.file_exists(skeleton_path):
		_fail("skeleton file not found: %s" % skeleton_path)
		return
	if not FileAccess.file_exists(atlas_path):
		_fail("atlas file not found: %s" % atlas_path)
		return

	var runtime_path := _runtime_skeleton_path(skeleton_path)
	if runtime_path.is_empty():
		_fail("could not stage JSON input for the runtime")
		return
	var skeleton_file := SpineSkeletonFileResource.new()
	skeleton_file.load_from_file(runtime_path)
	var atlas := SpineAtlasResource.new()
	atlas.load_from_atlas_file(atlas_path)
	var skeleton_data := SpineSkeletonDataResource.new()
	skeleton_data.skeleton_file_res = skeleton_file
	skeleton_data.atlas_res = atlas
	var sprite := SpineSprite.new()
	sprite.skeleton_data_res = skeleton_data
	root.add_child(sprite)

	var skeleton := sprite.get_skeleton()
	if skeleton == null or skeleton.get_data() == null:
		_fail("runtime did not create skeleton data")
		return
	var animations = skeleton.get_data().get_animations()
	if animations.is_empty():
		_fail("skeleton loaded without animations")
		return
	print("VALID animations=%d bounds=%s" % [animations.size(), skeleton.get_bounds()])
	quit(0)

func _runtime_skeleton_path(source: String) -> String:
	if source.get_extension().to_lower() != "json":
		return source
	var destination := ProjectSettings.globalize_path("user://validation_input.spjson")
	var input := FileAccess.open(source, FileAccess.READ)
	if input == null:
		return ""
	var output := FileAccess.open(destination, FileAccess.WRITE)
	if output == null:
		return ""
	output.store_buffer(input.get_buffer(input.get_length()))
	return destination

func _fail(message: String) -> void:
	push_error("INVALID %s" % message)
	quit(2)
