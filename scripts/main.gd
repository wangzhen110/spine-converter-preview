extends Control

const TARGET_VERSIONS := ["4.2.11", "4.1.24", "4.0.64", "3.8.99"]
const PREVIEW_RUNTIME_VERSION := "4.2.11"
const SETTINGS_PATH := "user://settings.cfg"

var input_path := ""
var output_path := ""
var atlas_path := ""
var original_version := "待检测"
var converted_version := ""
var spine_sprite: SpineSprite
var animations: Array = []
var animation_index := 0
var animation_timer := 0.0
var auto_play := false

var path_edit: LineEdit
var version_select: OptionButton
var format_select: OptionButton
var convert_button: Button
var batch_button: Button
var folder_import_button: Button
var batch_export_button: Button
var save_button: Button
var status_label: Label
var version_label: Label
var preview_surface: Control
var preview_root: Node2D
var empty_label: Label
var animation_select: OptionButton
var play_button: Button
var auto_button: CheckButton
var fit_button: CheckButton
var scale_slider: HSlider
var details_label: Label
var file_dialog: FileDialog
var batch_dialog: FileDialog
var save_dialog: FileDialog
var directory_dialog: FileDialog
var import_directory_dialog: FileDialog
var default_path_edit: LineEdit
var default_save_dir := ""
var auto_fit := true
var preview_offset := Vector2(0, 18)
var preview_bounds := Rect2()
var model_paths: Array[String] = []
var model_index := -1
var previous_model_button: Button
var next_model_button: Button
var model_counter_label: Label

func _ready() -> void:
	_load_settings()
	build_interface()
	get_window().files_dropped.connect(_on_files_dropped)
	preview_surface.resized.connect(_on_preview_resized)
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--batch-smoke-test="):
			_run_batch_smoke_test.call_deferred(argument.trim_prefix("--batch-smoke-test="))
			break
		if argument.begins_with("--smoke-test="):
			_run_smoke_test.call_deferred(argument.trim_prefix("--smoke-test="))
			break
		if argument.begins_with("--input="):
			_on_file_selected(argument.trim_prefix("--input="))
			_convert_and_preview.call_deferred()
			break
		if argument.begins_with("--folder="):
			_on_import_directory_selected.call_deferred(argument.trim_prefix("--folder="))
			break

func _run_smoke_test(source: String) -> void:
	if not FileAccess.file_exists(source):
		push_error("SMOKE_TEST source not found: %s" % source)
		get_tree().quit(2)
		return
	var converter := get_converter_path()
	if not FileAccess.file_exists(converter):
		push_error("SMOKE_TEST converter not found: %s" % converter)
		get_tree().quit(2)
		return
	var cache_dir := ProjectSettings.globalize_path("user://smoke_test")
	DirAccess.make_dir_recursive_absolute(cache_dir)
	var destination := cache_dir.path_join("converted-v4_2_11.skel")
	var output: Array = []
	var exit_code := OS.execute(converter, [source, destination, "-v", "4.2.11"], output, true, false)
	if exit_code != 0 or not FileAccess.file_exists(destination):
		push_error("SMOKE_TEST conversion failed (%d): %s" % [exit_code, "\n".join(output)])
		get_tree().quit(2)
		return
	var atlas := find_atlas_path(source)
	if atlas.is_empty():
		push_error("SMOKE_TEST atlas not found: %s" % source)
		get_tree().quit(2)
		return
	input_path = source
	original_version = _detect_original_version(source)
	converted_version = "4.2.11"
	load_preview(destination, atlas, output)
	if spine_sprite == null or animations.is_empty():
		push_error("SMOKE_TEST preview has no animations")
		get_tree().quit(2)
		return
	print("SMOKE_TEST PASS animations=%d output=%s" % [animations.size(), destination])
	get_tree().quit(0)

func _run_batch_smoke_test(specification: String) -> void:
	var separator := specification.find("|")
	if separator <= 0 or separator >= specification.length() - 1:
		push_error("BATCH_SMOKE_TEST expects <source-folder>|<output-folder>")
		get_tree().quit(2)
		return
	var source_folder := specification.substr(0, separator)
	var export_folder := specification.substr(separator + 1)
	if not DirAccess.dir_exists_absolute(source_folder):
		push_error("BATCH_SMOKE_TEST source folder not found: %s" % source_folder)
		get_tree().quit(2)
		return
	DirAccess.make_dir_recursive_absolute(export_folder)
	default_save_dir = export_folder
	default_path_edit.text = export_folder
	_on_import_directory_selected(source_folder)
	if model_paths.size() < 2:
		push_error("BATCH_SMOKE_TEST expected at least 2 models, found %d" % model_paths.size())
		get_tree().quit(2)
		return
	var first_model := model_paths[0]
	if original_version.get_slice(".", 0) != "3" or spine_sprite == null or animations.is_empty():
		push_error("BATCH_SMOKE_TEST first model did not detect/preview correctly: %s" % first_model)
		get_tree().quit(2)
		return
	_change_model(1)
	if model_index != 1 or model_paths[model_index] == first_model or spine_sprite == null or animations.is_empty():
		push_error("BATCH_SMOKE_TEST model navigation or second preview failed")
		get_tree().quit(2)
		return
	await _convert_files_batch(PackedStringArray(model_paths))
	var exported_count := 0
	var output_directory := DirAccess.open(export_folder)
	if output_directory != null:
		for file_name in output_directory.get_files():
			if file_name.ends_with("-v4_2_11.skel"):
				exported_count += 1
	if exported_count != model_paths.size():
		push_error("BATCH_SMOKE_TEST exported %d of %d models" % [exported_count, model_paths.size()])
		get_tree().quit(2)
		return
	print("BATCH_SMOKE_TEST PASS models=%d exported=%d navigation=PASS version=%s" % [model_paths.size(), exported_count, original_version])
	get_tree().quit(0)

func _process(delta: float) -> void:
	if not auto_play or animations.is_empty() or spine_sprite == null:
		return
	animation_timer -= delta
	if animation_timer <= 0.0:
		play_animation(animation_index + 1)

func build_interface() -> void:
	var background := ColorRect.new()
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	background.color = Color("0e1116")
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(background)

	var root := VBoxContainer.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT, Control.PRESET_MODE_MINSIZE, 24)
	root.add_theme_constant_override("separation", 16)
	add_child(root)

	var header := HBoxContainer.new()
	header.custom_minimum_size.y = 52
	root.add_child(header)
	var heading := Label.new()
	heading.text = "Spine 一键转换预览"
	heading.add_theme_font_size_override("font_size", 26)
	heading.add_theme_color_override("font_color", Color("f0e7d2"))
	header.add_child(heading)
	var edition_badge := Label.new()
	edition_badge.text = "源码公开 · 非商用版"
	edition_badge.add_theme_color_override("font_color", Color("85c7a2"))
	header.add_child(edition_badge)
	var header_space := Control.new()
	header_space.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(header_space)
	if ResourceLoader.exists("res://branding/support-qr.png"):
		var support_button := Button.new()
		support_button.text = "支持项目"
		support_button.tooltip_text = "查看自愿赞助二维码"
		support_button.pressed.connect(_show_support_dialog)
		header.add_child(support_button)

	var source_title := Label.new()
	source_title.text = "1. 选择源文件"
	source_title.add_theme_color_override("font_color", Color("b8c2cf"))
	root.add_child(source_title)
	var source_row := HBoxContainer.new()
	source_row.add_theme_constant_override("separation", 10)
	root.add_child(source_row)
	path_edit = LineEdit.new()
	path_edit.placeholder_text = "选择或拖入 .skel / .json 文件"
	path_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	path_edit.custom_minimum_size.y = 42
	path_edit.text_changed.connect(_on_path_changed)
	source_row.add_child(path_edit)
	var browse_button := Button.new()
	browse_button.text = "选择文件"
	browse_button.custom_minimum_size = Vector2(100, 42)
	browse_button.pressed.connect(_open_file_dialog)
	source_row.add_child(browse_button)
	batch_button = Button.new()
	batch_button.text = "导入多个文件"
	batch_button.custom_minimum_size = Vector2(100, 42)
	batch_button.pressed.connect(_open_batch_dialog)
	source_row.add_child(batch_button)
	folder_import_button = Button.new()
	folder_import_button.text = "导入文件夹"
	folder_import_button.custom_minimum_size = Vector2(110, 42)
	folder_import_button.pressed.connect(_open_import_directory_dialog)
	source_row.add_child(folder_import_button)
	var clear_selection_button := Button.new()
	clear_selection_button.text = "清空"
	clear_selection_button.tooltip_text = "清除当前文件、文件夹列表和预览"
	clear_selection_button.custom_minimum_size = Vector2(72, 42)
	clear_selection_button.pressed.connect(_clear_selection)
	source_row.add_child(clear_selection_button)
	var conversion_row := HBoxContainer.new()
	conversion_row.add_theme_constant_override("separation", 10)
	root.add_child(conversion_row)
	version_label = Label.new()
	version_label.text = "2. 原始版本：待检测"
	version_label.custom_minimum_size = Vector2(210, 42)
	version_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	version_label.add_theme_font_size_override("font_size", 17)
	version_label.add_theme_color_override("font_color", Color("d9c58d"))
	conversion_row.add_child(version_label)
	var target_version_label := Label.new()
	target_version_label.text = "转换为"
	target_version_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	conversion_row.add_child(target_version_label)
	version_select = OptionButton.new()
	version_select.custom_minimum_size = Vector2(110, 42)
	for version in TARGET_VERSIONS:
		version_select.add_item(version)
	version_select.item_selected.connect(_on_target_version_changed)
	conversion_row.add_child(version_select)
	format_select = OptionButton.new()
	format_select.custom_minimum_size = Vector2(90, 42)
	format_select.add_item("SKEL")
	format_select.add_item("JSON")
	conversion_row.add_child(format_select)
	convert_button = Button.new()
	convert_button.text = "转换并预览"
	convert_button.custom_minimum_size = Vector2(138, 42)
	convert_button.disabled = true
	convert_button.pressed.connect(_convert_and_preview)
	conversion_row.add_child(convert_button)
	batch_export_button = Button.new()
	batch_export_button.text = "批量导出"
	batch_export_button.custom_minimum_size = Vector2(105, 42)
	batch_export_button.disabled = true
	batch_export_button.pressed.connect(_export_imported_models)
	conversion_row.add_child(batch_export_button)
	var conversion_space := Control.new()
	conversion_space.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	conversion_row.add_child(conversion_space)
	var output_row := HBoxContainer.new()
	output_row.add_theme_constant_override("separation", 10)
	root.add_child(output_row)
	var output_title := Label.new()
	output_title.text = "3. 保存到"
	output_title.custom_minimum_size.x = 90
	output_title.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	output_row.add_child(output_title)
	default_path_edit = LineEdit.new()
	default_path_edit.text = default_save_dir
	default_path_edit.placeholder_text = "未设置时使用源文件目录"
	default_path_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	default_path_edit.custom_minimum_size.y = 42
	default_path_edit.text_submitted.connect(_on_default_path_changed)
	default_path_edit.focus_exited.connect(_save_default_path_from_edit)
	output_row.add_child(default_path_edit)
	var choose_directory_button := Button.new()
	choose_directory_button.text = "选择目录"
	choose_directory_button.custom_minimum_size.y = 42
	choose_directory_button.pressed.connect(_open_directory_dialog)
	output_row.add_child(choose_directory_button)
	save_button = Button.new()
	save_button.text = "保存结果"
	save_button.custom_minimum_size = Vector2(105, 42)
	save_button.disabled = true
	save_button.pressed.connect(_open_save_dialog)
	output_row.add_child(save_button)

	status_label = Label.new()
	status_label.text = "等待选择文件"
	status_label.custom_minimum_size.y = 48
	status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	status_label.max_lines_visible = 2
	status_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	status_label.clip_text = true
	status_label.add_theme_color_override("font_color", Color("9aa3af"))
	root.add_child(status_label)

	var content := HSplitContainer.new()
	content.size_flags_vertical = Control.SIZE_EXPAND_FILL
	content.split_offset = 650
	root.add_child(content)

	var preview_panel := PanelContainer.new()
	preview_panel.custom_minimum_size.x = 480
	var preview_style := StyleBoxFlat.new()
	preview_style.bg_color = Color("151b24")
	preview_style.border_color = Color("6f7f92")
	preview_style.set_border_width_all(2)
	preview_style.corner_radius_top_left = 6
	preview_style.corner_radius_top_right = 6
	preview_style.corner_radius_bottom_left = 6
	preview_style.corner_radius_bottom_right = 6
	preview_style.content_margin_left = 12
	preview_style.content_margin_right = 12
	preview_style.content_margin_top = 12
	preview_style.content_margin_bottom = 12
	preview_panel.add_theme_stylebox_override("panel", preview_style)
	content.add_child(preview_panel)
	preview_surface = Control.new()
	preview_surface.custom_minimum_size = Vector2(480, 320)
	preview_surface.clip_contents = true
	preview_panel.add_child(preview_surface)
	var frame_hint := Label.new()
	frame_hint.text = "预览区域"
	frame_hint.position = Vector2(12, 8)
	frame_hint.add_theme_color_override("font_color", Color("9caabd"))
	frame_hint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	preview_surface.add_child(frame_hint)
	preview_root = Node2D.new()
	preview_surface.add_child(preview_root)
	previous_model_button = Button.new()
	previous_model_button.text = "‹"
	previous_model_button.tooltip_text = "上一个模型"
	previous_model_button.custom_minimum_size = Vector2(52, 72)
	previous_model_button.add_theme_font_size_override("font_size", 28)
	previous_model_button.set_anchors_preset(Control.PRESET_CENTER_LEFT)
	previous_model_button.position = Vector2(12, -32)
	previous_model_button.visible = false
	previous_model_button.pressed.connect(_change_model.bind(-1))
	preview_surface.add_child(previous_model_button)
	next_model_button = Button.new()
	next_model_button.text = "›"
	next_model_button.tooltip_text = "下一个模型"
	next_model_button.custom_minimum_size = Vector2(52, 72)
	next_model_button.add_theme_font_size_override("font_size", 28)
	next_model_button.set_anchors_preset(Control.PRESET_CENTER_RIGHT)
	next_model_button.position = Vector2(-56, -32)
	next_model_button.visible = false
	next_model_button.pressed.connect(_change_model.bind(1))
	preview_surface.add_child(next_model_button)
	model_counter_label = Label.new()
	model_counter_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	model_counter_label.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	model_counter_label.position = Vector2(-300, 10)
	model_counter_label.size = Vector2(280, 28)
	model_counter_label.add_theme_color_override("font_color", Color("c8d2df"))
	preview_surface.add_child(model_counter_label)
	empty_label = Label.new()
	empty_label.text = "转换成功后在这里实时播放骨骼动画"
	empty_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	empty_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	empty_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	empty_label.add_theme_color_override("font_color", Color("697280"))
	empty_label.add_theme_font_size_override("font_size", 17)
	preview_surface.add_child(empty_label)

	var controls_scroll := ScrollContainer.new()
	controls_scroll.custom_minimum_size.x = 300
	controls_scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	controls_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	controls_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	content.add_child(controls_scroll)
	var controls := VBoxContainer.new()
	controls.custom_minimum_size.x = 280
	controls.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	controls.add_theme_constant_override("separation", 12)
	controls_scroll.add_child(controls)
	var control_title := Label.new()
	control_title.text = "动画控制"
	control_title.add_theme_font_size_override("font_size", 20)
	control_title.add_theme_color_override("font_color", Color("e3d7bc"))
	controls.add_child(control_title)
	animation_select = OptionButton.new()
	animation_select.custom_minimum_size.y = 42
	animation_select.disabled = true
	animation_select.item_selected.connect(_on_animation_selected)
	controls.add_child(animation_select)
	var playback_row := HBoxContainer.new()
	playback_row.add_theme_constant_override("separation", 8)
	controls.add_child(playback_row)
	for spec in [["上一个", -1], ["重播", 0], ["下一个", 1]]:
		var button := Button.new()
		button.text = spec[0]
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.pressed.connect(_step_animation.bind(spec[1]))
		playback_row.add_child(button)
	play_button = Button.new()
	play_button.text = "暂停"
	play_button.disabled = true
	play_button.pressed.connect(_toggle_pause)
	controls.add_child(play_button)
	auto_button = CheckButton.new()
	auto_button.text = "自动轮播全部动画"
	auto_button.disabled = true
	auto_button.toggled.connect(_toggle_auto_play)
	controls.add_child(auto_button)
	var scale_title := Label.new()
	scale_title.text = "预览缩放"
	controls.add_child(scale_title)
	fit_button = CheckButton.new()
	fit_button.text = "自动适配预览框"
	fit_button.button_pressed = true
	fit_button.toggled.connect(_toggle_auto_fit)
	controls.add_child(fit_button)
	scale_slider = HSlider.new()
	scale_slider.min_value = 0.1
	scale_slider.max_value = 1.5
	scale_slider.step = 0.05
	scale_slider.value = 0.6
	scale_slider.drag_started.connect(_on_scale_drag_started)
	scale_slider.value_changed.connect(_set_preview_scale)
	controls.add_child(scale_slider)
	details_label = Label.new()
	details_label.text = "尚未加载角色"
	details_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	details_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	details_label.add_theme_color_override("font_color", Color("9aa3af"))
	controls.add_child(details_label)

	file_dialog = FileDialog.new()
	file_dialog.use_native_dialog = true
	file_dialog.title = "选择 Spine 文件"
	file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	file_dialog.access = FileDialog.ACCESS_FILESYSTEM
	file_dialog.filters = PackedStringArray(["*.skel,*.json ; Spine files", "*.skel ; Spine binary", "*.json ; Spine JSON"])
	file_dialog.file_selected.connect(_on_file_selected)
	add_child(file_dialog)
	batch_dialog = FileDialog.new()
	batch_dialog.use_native_dialog = true
	batch_dialog.title = "批量选择 Spine 文件"
	batch_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILES
	batch_dialog.access = FileDialog.ACCESS_FILESYSTEM
	batch_dialog.filters = PackedStringArray(["*.skel,*.json ; Spine files", "*.skel ; Spine binary", "*.json ; Spine JSON"])
	batch_dialog.files_selected.connect(_on_batch_files_selected)
	add_child(batch_dialog)
	save_dialog = FileDialog.new()
	save_dialog.use_native_dialog = true
	save_dialog.title = "保存转换结果"
	save_dialog.file_mode = FileDialog.FILE_MODE_SAVE_FILE
	save_dialog.access = FileDialog.ACCESS_FILESYSTEM
	save_dialog.file_selected.connect(_on_save_file_selected)
	add_child(save_dialog)
	directory_dialog = FileDialog.new()
	directory_dialog.use_native_dialog = true
	directory_dialog.title = "选择默认保存目录"
	directory_dialog.file_mode = FileDialog.FILE_MODE_OPEN_DIR
	directory_dialog.access = FileDialog.ACCESS_FILESYSTEM
	directory_dialog.dir_selected.connect(_on_default_directory_selected)
	add_child(directory_dialog)
	import_directory_dialog = FileDialog.new()
	import_directory_dialog.use_native_dialog = true
	import_directory_dialog.title = "选择 Spine 素材文件夹"
	import_directory_dialog.file_mode = FileDialog.FILE_MODE_OPEN_DIR
	import_directory_dialog.access = FileDialog.ACCESS_FILESYSTEM
	import_directory_dialog.dir_selected.connect(_on_import_directory_selected)
	add_child(import_directory_dialog)

func _open_file_dialog() -> void:
	file_dialog.popup_centered_ratio(0.72)

func _open_batch_dialog() -> void:
	batch_dialog.popup_centered_ratio(0.78)

func _open_import_directory_dialog() -> void:
	import_directory_dialog.popup_centered_ratio(0.72)

func _open_save_dialog() -> void:
	if output_path.is_empty() or not FileAccess.file_exists(output_path):
		set_status("没有可保存的转换结果", true)
		return
	var save_dir := default_save_dir if DirAccess.dir_exists_absolute(default_save_dir) else output_path.get_base_dir()
	save_dialog.current_dir = save_dir
	save_dialog.current_file = output_path.get_file()
	var extension := output_path.get_extension().to_lower()
	save_dialog.filters = PackedStringArray(["*.%s ; Spine %s" % [extension, extension.to_upper()]])
	save_dialog.popup_centered_ratio(0.72)

func _open_directory_dialog() -> void:
	if DirAccess.dir_exists_absolute(default_save_dir):
		directory_dialog.current_dir = default_save_dir
	directory_dialog.popup_centered_ratio(0.72)

func _on_save_file_selected(path: String) -> void:
	var destination := path
	if destination.get_extension().is_empty():
		destination += "." + output_path.get_extension()
	if destination.simplify_path() == output_path.simplify_path():
		set_status("转换结果已保存在：%s" % destination)
		return
	var error := DirAccess.copy_absolute(output_path, destination)
	if error != OK:
		set_status("保存失败，错误码：%d" % error, true)
		return
	default_save_dir = destination.get_base_dir()
	default_path_edit.text = default_save_dir
	_save_settings()
	set_status("已保存到：%s" % destination)

func _on_default_directory_selected(path: String) -> void:
	default_save_dir = path.simplify_path()
	default_path_edit.text = default_save_dir
	_save_settings()
	set_status("默认保存目录已设置：%s" % default_save_dir)

func _on_default_path_changed(path: String) -> void:
	default_save_dir = path.strip_edges().simplify_path()
	_save_settings()

func _save_default_path_from_edit() -> void:
	_on_default_path_changed(default_path_edit.text)

func _load_settings() -> void:
	var config := ConfigFile.new()
	if config.load(SETTINGS_PATH) == OK:
		default_save_dir = str(config.get_value("save", "default_directory", ""))

func _save_settings() -> void:
	var config := ConfigFile.new()
	config.set_value("save", "default_directory", default_save_dir)
	config.save(SETTINGS_PATH)

func _on_file_selected(path: String) -> void:
	_clear_model_collection()
	path_edit.text = path
	_on_path_changed(path)

func _on_files_dropped(files: PackedStringArray) -> void:
	var valid_files := PackedStringArray()
	for file in files:
		if DirAccess.dir_exists_absolute(file):
			var discovered: Array[String] = []
			_scan_spine_files(file, discovered)
			for source in discovered:
				valid_files.append(source)
			continue
		if file.get_extension().to_lower() in ["skel", "json"]:
			valid_files.append(file)
	if valid_files.size() == 1:
		_on_file_selected(valid_files[0])
		return
	if valid_files.size() > 1:
		_on_batch_files_selected(valid_files)
		return
	set_status("拖入的文件不是 .skel 或 .json", true)

func _on_batch_files_selected(files: PackedStringArray) -> void:
	_import_model_collection(Array(files))

func _on_import_directory_selected(path: String) -> void:
	var discovered: Array[String] = []
	_scan_spine_files(path, discovered)
	_import_model_collection(discovered)

func _scan_spine_files(directory_path: String, results: Array[String]) -> void:
	var directory := DirAccess.open(directory_path)
	if directory == null:
		return
	for file_name in directory.get_files():
		if file_name.get_extension().to_lower() in ["skel", "json"] and not _is_generated_output(file_name):
			results.append(directory_path.path_join(file_name))
	for child_directory in directory.get_directories():
		if child_directory.begins_with("."):
			continue
		_scan_spine_files(directory_path.path_join(child_directory), results)

func _is_generated_output(file_name: String) -> bool:
	var generated_pattern := RegEx.new()
	generated_pattern.compile("-v[0-9]+(?:_[0-9]+){1,2}$")
	return generated_pattern.search(file_name.get_basename()) != null

func _import_model_collection(paths: Array) -> void:
	var unique_models: Dictionary = {}
	for value in paths:
		var source := str(value)
		if not FileAccess.file_exists(source) or source.get_extension().to_lower() not in ["skel", "json"]:
			continue
		if _is_generated_output(source.get_file()):
			continue
		var key := source.get_base_dir().path_join(source.get_file().get_basename()).to_lower()
		if not unique_models.has(key) or source.get_extension().to_lower() == "skel":
			unique_models[key] = source
	model_paths.clear()
	for source in unique_models.values():
		model_paths.append(str(source))
	model_paths.sort()
	if model_paths.is_empty():
		_clear_model_collection()
		set_status("文件夹中没有找到可用的 Spine 模型", true)
		return
	model_index = 0
	batch_export_button.disabled = false
	_update_model_navigation()
	_preview_imported_model()

func _clear_model_collection() -> void:
	model_paths.clear()
	model_index = -1
	if previous_model_button != null:
		previous_model_button.visible = false
		next_model_button.visible = false
		model_counter_label.text = ""
	if batch_export_button != null:
		batch_export_button.disabled = true

func _clear_selection() -> void:
	_clear_model_collection()
	clear_preview()
	input_path = ""
	output_path = ""
	atlas_path = ""
	original_version = "待检测"
	converted_version = ""
	path_edit.set_text("")
	convert_button.disabled = true
	save_button.disabled = true
	animation_select.disabled = true
	play_button.disabled = true
	auto_button.set_pressed_no_signal(false)
	auto_button.disabled = true
	auto_play = false
	preview_bounds = Rect2()
	empty_label.visible = true
	details_label.text = "尚未加载角色"
	_update_version_label()
	set_status("已清空，可以重新选择文件或文件夹")

func _change_model(step: int) -> void:
	if model_paths.is_empty():
		return
	model_index = wrapi(model_index + step, 0, model_paths.size())
	_update_model_navigation()
	_preview_imported_model()

func _update_model_navigation() -> void:
	var has_multiple := model_paths.size() > 1
	previous_model_button.visible = has_multiple
	next_model_button.visible = has_multiple
	if model_index >= 0 and model_index < model_paths.size():
		model_counter_label.text = "%d / %d   %s" % [model_index + 1, model_paths.size(), model_paths[model_index].get_file()]

func _preview_imported_model() -> void:
	if model_index < 0 or model_index >= model_paths.size():
		return
	var source := model_paths[model_index]
	var converter := get_converter_path()
	var target_version: String = TARGET_VERSIONS[version_select.selected]
	set_status("正在准备预览：%s" % source.get_file())
	var converter_log: Array = []
	var preview_path := _create_preview_staging(source, converter_log)
	if preview_path.is_empty():
		set_status("模型预览转换失败：%s" % source.get_file(), true)
		return
	path_edit.text = source
	input_path = source
	original_version = _detect_original_version(source)
	converted_version = target_version
	_update_version_label()
	atlas_path = find_atlas_path(source)
	if atlas_path.is_empty():
		clear_preview()
		set_status("已导入，但找不到 atlas，无法预览：%s" % source.get_file(), true)
		return
	load_preview(preview_path, atlas_path, converter_log)
	_update_model_navigation()

func _export_imported_models() -> void:
	if model_paths.is_empty():
		set_status("请先导入模型文件夹", true)
		return
	await _convert_files_batch(PackedStringArray(model_paths))

func _convert_files_batch(files: PackedStringArray) -> void:
	var converter := get_converter_path()
	if not FileAccess.file_exists(converter):
		set_status("找不到转换器：%s" % converter, true)
		return
	var valid_files: Array[String] = []
	for file in files:
		if FileAccess.file_exists(file) and file.get_extension().to_lower() in ["skel", "json"]:
			valid_files.append(file)
	if valid_files.is_empty():
		set_status("没有可转换的 .skel 或 .json 文件", true)
		return
	batch_button.disabled = true
	convert_button.disabled = true
	save_button.disabled = true
	var target_version: String = TARGET_VERSIONS[version_select.selected]
	var target_extension := "skel" if format_select.selected == 0 else "json"
	var suffix: String = target_version.replace(".", "_")
	var success_count := 0
	var failure_lines: Array[String] = []
	var last_source := ""
	var last_output := ""
	var last_log: Array = []
	for index in valid_files.size():
		var source: String = valid_files[index]
		var output_dir := default_save_dir if DirAccess.dir_exists_absolute(default_save_dir) else source.get_base_dir()
		var destination := output_dir.path_join("%s-v%s.%s" % [source.get_file().get_basename(), suffix, target_extension])
		set_status("批量转换 %d/%d：%s" % [index + 1, valid_files.size(), source.get_file()])
		await get_tree().process_frame
		var converter_log: Array = []
		var exit_code := OS.execute(converter, [source, destination, "-v", target_version], converter_log, true, false)
		if exit_code == 0 and FileAccess.file_exists(destination):
			_write_conversion_report(source, destination, target_version, converter_log)
			success_count += 1
			last_source = source
			last_output = destination
			last_log = converter_log
		else:
			failure_lines.append("%s（错误码 %d）" % [source.get_file(), exit_code])
	batch_button.disabled = false
	convert_button.disabled = not FileAccess.file_exists(input_path)
	var summary := "批量转换完成：成功 %d，失败 %d，共 %d 个文件" % [success_count, failure_lines.size(), valid_files.size()]
	if success_count > 0:
		path_edit.text = last_source
		input_path = last_source
		output_path = last_output
		original_version = _detect_original_version(last_source)
		converted_version = target_version
		_update_version_label()
		save_button.disabled = false
		atlas_path = find_atlas_path(last_source)
		if not atlas_path.is_empty():
			load_preview(last_output, atlas_path, last_log)
	var failure_detail := ""
	if not failure_lines.is_empty():
		failure_detail = "\n\n失败文件：\n" + "\n".join(failure_lines.slice(0, 20))
	details_label.text = summary + failure_detail + "\n\n" + details_label.text
	set_status(summary, success_count == 0)

func _on_path_changed(path: String) -> void:
	input_path = path.strip_edges()
	save_button.disabled = true
	original_version = _detect_original_version(input_path)
	converted_version = TARGET_VERSIONS[version_select.selected]
	_update_version_label()
	convert_button.disabled = not FileAccess.file_exists(input_path) or input_path.get_extension().to_lower() not in ["skel", "json"]
	if convert_button.disabled:
		output_path = ""
		atlas_path = ""
		clear_preview()
		empty_label.visible = true
		set_status("请选择有效的 .skel 或 .json 文件", true)
	else:
		set_status("文件已就绪，点击“转换并预览”")

func _convert_and_preview() -> void:
	var converter := get_converter_path()
	if not FileAccess.file_exists(converter):
		set_status("找不到转换器：%s" % converter, true)
		return
	var target_version: String = TARGET_VERSIONS[version_select.selected]
	var extension := "skel" if format_select.selected == 0 else "json"
	var suffix: String = target_version.replace(".", "_")
	var output_dir := default_save_dir if DirAccess.dir_exists_absolute(default_save_dir) else input_path.get_base_dir()
	output_path = output_dir.path_join("%s-v%s.%s" % [input_path.get_file().get_basename(), suffix, extension])
	convert_button.disabled = true
	set_status("正在转换到 Spine %s..." % target_version)
	var output: Array = []
	var exit_code := OS.execute(converter, [input_path, output_path, "-v", target_version], output, true, false)
	convert_button.disabled = false
	if original_version in ["待检测", "未知"]:
		original_version = _extract_original_version(output)
	converted_version = target_version
	_update_version_label()
	if exit_code != 0 or not FileAccess.file_exists(output_path):
		set_status("转换失败，退出码 %d\n%s" % [exit_code, "\n".join(output)], true)
		return
	_write_conversion_report(input_path, output_path, target_version, output)
	save_button.disabled = false
	atlas_path = find_atlas_path(input_path)
	if atlas_path.is_empty():
		set_status("转换成功，但未找到同名 .atlas，无法预览", true)
		details_label.text = "输出：%s\n\n转换日志：\n%s" % [output_path, "\n".join(output)]
		return
	set_status("转换完成，正在准备 %s 预览暂存..." % PREVIEW_RUNTIME_VERSION)
	var preview_log: Array = []
	var preview_path := _create_preview_staging(input_path, preview_log)
	if preview_path.is_empty():
		set_status("目标文件已生成，但预览暂存转换失败", true)
		details_label.text = "输出：%s\n\n转换日志：\n%s" % [output_path, "\n".join(output)]
		return
	load_preview(preview_path, atlas_path, output + preview_log)

func _create_preview_staging(source: String, converter_log: Array) -> String:
	var cache_dir := ProjectSettings.globalize_path("user://preview_cache")
	DirAccess.make_dir_recursive_absolute(cache_dir)
	var preview_path := cache_dir.path_join("%s-v%s.skel" % [str(source.hash()), PREVIEW_RUNTIME_VERSION.replace(".", "_")])
	var exit_code := OS.execute(
		get_converter_path(),
		[source, preview_path, "-v", PREVIEW_RUNTIME_VERSION],
		converter_log,
		true,
		false
	)
	return preview_path if exit_code == 0 and FileAccess.file_exists(preview_path) else ""

func _write_conversion_report(source: String, destination: String, target_version: String, converter_log: Array) -> void:
	var source_version := _detect_original_version(source)
	var source_line := _version_line(source_version)
	var target_line := _version_line(target_version)
	var warnings: Array[String] = []
	if source_line > target_line:
		warnings.append("这是降级转换。新版字段若在旧版格式中没有对应项，可能被移除或近似表达。")
	if source_line >= 40 and target_line <= 38:
		warnings.append("4.x 到 3.8：贝塞尔曲线会转换为 3.x 相对控制点，旋转关键帧会改写为最短路径语义。")
		warnings.append("4.x 的比例路径间距会转换为 3.x 的长度间距。")
	if source_line >= 42 and target_line <= 41:
		warnings.append("4.2 到旧版：约束顺序会重新映射。请重点检查多个约束相互依赖的动画。")
	if source_line >= 42 and target_line < 42:
		warnings.append("4.2 物理约束在旧版中没有等价能力；包含物理时间线时可能丢失。")
	if source_line < target_line:
		warnings.append("这是升级转换。旧版数据会映射到新版结构，但不会自动获得新版专属功能。")
	if warnings.is_empty():
		warnings.append("未检测到已知的跨大版本损失规则；仍建议在目标 Spine Runtime 中逐个检查动画。")
	var report := {
		"schema": "spine-converter-report/1",
		"source_file": source.get_file(),
		"output_file": destination.get_file(),
		"source_version": source_version,
		"target_version": target_version,
		"source_format": source.get_extension().to_lower(),
		"target_format": destination.get_extension().to_lower(),
		"direction": "downgrade" if source_line > target_line else ("upgrade" if source_line < target_line else "same-version"),
		"warnings": warnings,
		"converter_log": converter_log.map(func(line): return str(line)),
	}
	var report_path := destination + ".report.json"
	var file := FileAccess.open(report_path, FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify(report, "  "))

func _version_line(version: String) -> int:
	var parts := version.split(".")
	if parts.size() < 2 or not parts[0].is_valid_int() or not parts[1].is_valid_int():
		return -1
	return int(parts[0]) * 10 + int(parts[1])

func find_atlas_path(source: String) -> String:
	var direct := source.get_basename() + ".atlas"
	if FileAccess.file_exists(direct):
		return direct
	var directory := DirAccess.open(source.get_base_dir())
	if directory == null:
		return ""
	for file in directory.get_files():
		if file.get_extension().to_lower() == "atlas":
			return source.get_base_dir().path_join(file)
	return ""

func load_preview(skeleton_path: String, atlas_file: String, converter_output: Array) -> void:
	clear_preview()
	var runtime_path := _runtime_skeleton_path(skeleton_path)
	if runtime_path.is_empty():
		set_status("无法准备预览文件：%s" % skeleton_path, true)
		return
	var skeleton_file := SpineSkeletonFileResource.new()
	skeleton_file.load_from_file(runtime_path)
	var atlas := SpineAtlasResource.new()
	atlas.load_from_atlas_file(atlas_file)
	var skeleton_data := SpineSkeletonDataResource.new()
	skeleton_data.skeleton_file_res = skeleton_file
	skeleton_data.atlas_res = atlas
	spine_sprite = SpineSprite.new()
	spine_sprite.skeleton_data_res = skeleton_data
	spine_sprite.scale = Vector2.ONE * scale_slider.value
	preview_root.add_child(spine_sprite)
	animations = spine_sprite.get_skeleton().get_data().get_animations()
	animation_select.clear()
	for animation: SpineAnimation in animations:
		animation_select.add_item(animation.get_name())
	animation_select.disabled = animations.is_empty()
	play_button.disabled = animations.is_empty()
	auto_button.disabled = animations.is_empty()
	empty_label.visible = animations.is_empty()
	_fit_preview()
	if animations.is_empty():
		set_status("骨骼已加载，但没有动画", true)
		return
	play_animation(0)
	details_label.text = "版本：%s  ->  %s\n\n输入：%s\n\n输出：%s\n\n图集：%s\n\n动画数量：%d\n\n转换器输出：\n%s" % [
		original_version,
		converted_version,
		input_path,
		skeleton_path,
		atlas_file,
		animations.size(),
		"\n".join(converter_output),
	]
	set_status("转换并加载成功：%d 个动画" % animations.size())

func _extract_original_version(converter_output: Array) -> String:
	for line in converter_output:
		var text_line := str(line)
		var marker := "Detected input Spine version:"
		var marker_index := text_line.find(marker)
		if marker_index >= 0:
			var remainder := text_line.substr(marker_index + marker.length()).strip_edges()
			return remainder.split("\n")[0].strip_edges()
	return "未知"

func _detect_original_version(path: String) -> String:
	if not FileAccess.file_exists(path):
		return "待检测"
	if path.get_extension().to_lower() == "json":
		var file := FileAccess.open(path, FileAccess.READ)
		if file == null:
			return "未知"
		var data = JSON.parse_string(file.get_as_text())
		if data is Dictionary:
			var skeleton = data.get("skeleton", {})
			if skeleton is Dictionary and skeleton.has("spine"):
				return str(skeleton["spine"])
		return "未知"
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return "未知"
	var prefix := file.get_buffer(mini(160, file.get_length()))
	var searchable := ""
	for byte in prefix:
		searchable += char(byte) if byte >= 32 and byte <= 126 else " "
	var version_regex := RegEx.new()
	version_regex.compile("[0-9]+\\.[0-9]+(?:\\.[0-9]+)?")
	var version_match := version_regex.search(searchable)
	return version_match.get_string() if version_match != null else "未知"

func _on_target_version_changed(_index: int) -> void:
	converted_version = TARGET_VERSIONS[version_select.selected]
	_update_version_label()

func _update_version_label() -> void:
	if version_label != null:
		version_label.text = "2. 原始版本：%s" % original_version

func clear_preview() -> void:
	if spine_sprite != null and is_instance_valid(spine_sprite):
		spine_sprite.queue_free()
	spine_sprite = null
	animations.clear()
	animation_select.clear()

func play_animation(index: int) -> void:
	if animations.is_empty() or spine_sprite == null:
		return
	animation_index = wrapi(index, 0, animations.size())
	var animation: SpineAnimation = animations[animation_index]
	spine_sprite.set_time_scale(1.0)
	spine_sprite.get_animation_state().set_animation(animation.get_name(), true, 0)
	animation_select.select(animation_index)
	animation_timer = maxf(animation.get_duration() + 0.75, 2.5)
	play_button.text = "暂停"

func _on_animation_selected(index: int) -> void:
	auto_button.button_pressed = false
	play_animation(index)

func _step_animation(step: int) -> void:
	if animations.is_empty():
		return
	auto_button.button_pressed = false
	play_animation(animation_index + step)

func _toggle_pause() -> void:
	if spine_sprite == null:
		return
	var pausing := spine_sprite.get_time_scale() > 0.0
	spine_sprite.set_time_scale(0.0 if pausing else 1.0)
	play_button.text = "继续" if pausing else "暂停"

func _toggle_auto_play(enabled: bool) -> void:
	auto_play = enabled
	if enabled and not animations.is_empty():
		play_animation(animation_index)

func _set_preview_scale(value: float) -> void:
	if spine_sprite != null:
		spine_sprite.scale = Vector2.ONE * value
		if preview_bounds.size.x > 0.0 and preview_bounds.size.y > 0.0:
			preview_offset = _get_centering_offset(preview_bounds, value)
			_center_preview()

func _on_scale_drag_started() -> void:
	auto_fit = false
	fit_button.set_pressed_no_signal(false)

func _toggle_auto_fit(enabled: bool) -> void:
	auto_fit = enabled
	if enabled:
		if preview_bounds.size.x > 0.0 and preview_bounds.size.y > 0.0:
			_apply_fit(preview_bounds)
		else:
			_fit_preview()

func _fit_preview() -> void:
	if spine_sprite == null or preview_surface == null:
		_center_preview()
		return
	var bounds: Rect2 = spine_sprite.get_skeleton().get_bounds()
	if bounds.size.x <= 0.0 or bounds.size.y <= 0.0:
		_center_preview()
		return
	preview_bounds = bounds
	_apply_fit(bounds)

func _apply_fit(bounds: Rect2) -> void:
	if spine_sprite == null or preview_surface == null:
		return
	var fit_scale := spine_sprite.scale.x
	if auto_fit:
		var available := preview_surface.size - Vector2(48, 64)
		fit_scale = minf(available.x / bounds.size.x, available.y / bounds.size.y) * 0.78
		fit_scale = clampf(fit_scale, 0.1, 3.0)
		scale_slider.set_value_no_signal(fit_scale)
		spine_sprite.scale = Vector2.ONE * fit_scale
	spine_sprite.position = Vector2.ZERO
	preview_offset = _get_centering_offset(bounds, fit_scale)
	_center_preview()

func _get_centering_offset(bounds: Rect2, preview_scale: float) -> Vector2:
	var center := bounds.position + bounds.size * 0.5
	return -center * preview_scale + Vector2(0, 18)

func _center_preview() -> void:
	if preview_root != null and preview_surface != null:
		preview_root.position = preview_surface.size * 0.5 + preview_offset

func _on_preview_resized() -> void:
	if auto_fit and preview_bounds.size.x > 0.0 and preview_bounds.size.y > 0.0:
		_apply_fit(preview_bounds)
	else:
		_center_preview()

func get_converter_path() -> String:
	if OS.has_feature("editor"):
		return ProjectSettings.globalize_path("res://tools/SpineConverter.exe")
	return OS.get_executable_path().get_base_dir().path_join("tools/SpineConverter.exe")

func _runtime_skeleton_path(source: String) -> String:
	if source.get_extension().to_lower() != "json":
		return source
	var cache_dir := ProjectSettings.globalize_path("user://preview_cache")
	DirAccess.make_dir_recursive_absolute(cache_dir)
	var destination := cache_dir.path_join("%s.spjson" % str(source.hash()))
	var error := DirAccess.copy_absolute(source, destination)
	return destination if error == OK else ""

func set_status(message: String, is_error := false) -> void:
	status_label.tooltip_text = message
	status_label.text = message.replace("\r", " ").replace("\n", " ")
	status_label.add_theme_color_override("font_color", Color("ef7777") if is_error else Color("85c7a2"))

func _show_support_dialog() -> void:
	var dialog := AcceptDialog.new()
	dialog.title = "支持项目"
	dialog.get_ok_button().text = "关闭"
	var content := VBoxContainer.new()
	content.position = Vector2(20, 48)
	content.size = Vector2(380, 570)
	content.add_theme_constant_override("separation", 12)
	var message := Label.new()
	message.text = "支付宝扫码赞助\n完整功能永久免费，赞助不影响任何功能。"
	message.custom_minimum_size = Vector2(380, 48)
	message.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	message.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	content.add_child(message)
	var qr := TextureRect.new()
	qr.texture = load("res://branding/support-qr.png")
	qr.custom_minimum_size = Vector2(340, 500)
	qr.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	qr.size_flags_vertical = Control.SIZE_EXPAND_FILL
	qr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	qr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	content.add_child(qr)
	dialog.add_child(content)
	dialog.confirmed.connect(dialog.queue_free)
	dialog.canceled.connect(dialog.queue_free)
	add_child(dialog)
	dialog.popup_centered(Vector2i(420, 680))
