extends SceneTree

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 1:
		_fail("usage: -- <manifest.tsv>")
		return
	var manifest := FileAccess.open(args[0], FileAccess.READ)
	if manifest == null:
		_fail("manifest not found: %s" % args[0])
		return
	var total := 0
	while not manifest.eof_reached():
		var line := manifest.get_line()
		if line.is_empty():
			continue
		var fields := line.split("\t", false, 2)
		if fields.size() != 2:
			_fail("invalid manifest line %d" % (total + 1))
			return
		if not _validate_model(fields[0], fields[1]):
			return
		total += 1
	print("VALID_BATCH models=%d" % total)
	quit(0)

func _validate_model(skeleton_path: String, atlas_path: String) -> bool:
	print("VALIDATING %s" % skeleton_path)
	var skeleton_file := SpineSkeletonFileResource.new()
	skeleton_file.load_from_file(skeleton_path)
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
		_fail("runtime did not create skeleton data: %s" % skeleton_path)
		return false
	if skeleton.get_data().get_animations().is_empty():
		_fail("skeleton loaded without animations: %s" % skeleton_path)
		return false
	root.remove_child(sprite)
	sprite.free()
	return true

func _fail(message: String) -> void:
	push_error("INVALID %s" % message)
	quit(2)
