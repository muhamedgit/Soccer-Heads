extends Node2D

@export var sky_color: Color = Color(0.22, 0.42, 0.78, 1.0)
@export var upper_sky_color: Color = Color(0.14, 0.28, 0.55, 1.0)

@export var stand_back_color: Color = Color(0.18, 0.20, 0.28, 1.0)
@export var stand_mid_color: Color = Color(0.24, 0.27, 0.36, 1.0)
@export var stand_front_color: Color = Color(0.30, 0.34, 0.44, 1.0)

@export var crowd_color_a: Color = Color(0.62, 0.66, 0.74, 1.0)
@export var crowd_color_b: Color = Color(0.52, 0.56, 0.64, 1.0)
@export var crowd_color_c: Color = Color(0.42, 0.46, 0.54, 1.0)

@export var rail_color: Color = Color(0.85, 0.85, 0.90, 0.75)
@export var field_color: Color = Color(0.16, 0.45, 0.20, 1.0)
@export var field_shadow_color: Color = Color(0.08, 0.20, 0.10, 0.45)

@export var line_color: Color = Color(0.95, 0.95, 0.95, 1.0)
@export var line_width: float = 3.0

func _ready() -> void:
	z_index = -100
	position = Vector2.ZERO
	scale = Vector2.ONE

	if get_viewport():
		get_viewport().size_changed.connect(_on_viewport_size_changed)

	queue_redraw()

func _on_viewport_size_changed() -> void:
	queue_redraw()

func _draw() -> void:
	var viewport_size := get_viewport_rect().size
	var w := viewport_size.x
	var h := viewport_size.y

	# Sky
	draw_rect(Rect2(0, 0, w, h), sky_color, true)
	draw_rect(Rect2(0, 0, w, h * 0.32), upper_sky_color, true)
	draw_rect(Rect2(0, h * 0.22, w, h * 0.10), Color(1, 1, 1, 0.05), true)

	# Stands
	draw_rect(Rect2(0, h * 0.28, w, h * 0.08), stand_back_color, true)
	draw_rect(Rect2(0, h * 0.36, w, h * 0.08), stand_mid_color, true)
	draw_rect(Rect2(0, h * 0.44, w, h * 0.10), stand_front_color, true)

	# Crowd rows
	_draw_crowd_row(h * 0.305, w, 0.10, 0.10, 4.0, 12.0)
	_draw_crowd_row(h * 0.385, w, 0.08, 0.08, 4.0, 12.0)
	_draw_crowd_row(h * 0.465, w, 0.06, 0.06, 4.5, 13.0)

	# Railings
	draw_rect(Rect2(0, h * 0.35, w, 3), rail_color, true)
	draw_rect(Rect2(0, h * 0.43, w, 3), rail_color, true)
	draw_rect(Rect2(0, h * 0.54, w, 4), rail_color, true)

	# Lower wall above field
	draw_rect(Rect2(0, h * 0.54, w, h * 0.05), Color(0.17, 0.19, 0.24, 1.0), true)

	# Field
	var field_top := h * 0.59
	var field_height := h * 0.41
	draw_rect(Rect2(0, field_top, w, field_height), field_color, true)
	draw_rect(Rect2(0, field_top, w, h * 0.012), field_shadow_color, true)

	# Main field area
	var margin_x := w * 0.08
	var margin_top := field_height * 0.12
	var margin_bottom := field_height * 0.10

	var playable_field := Rect2(
		margin_x,
		field_top + margin_top,
		w - margin_x * 2.0,
		field_height - margin_top - margin_bottom
	)

	# Outer field outline
	draw_rect(playable_field, line_color, false, line_width)

	# Center line
	var center_x := playable_field.position.x + playable_field.size.x / 2.0
	draw_line(
		Vector2(center_x, playable_field.position.y),
		Vector2(center_x, playable_field.position.y + playable_field.size.y),
		line_color,
		line_width
	)

	# Penalty boxes
	var box_depth := playable_field.size.x * 0.16
	var box_height := playable_field.size.y * 0.46
	var box_y := playable_field.position.y + (playable_field.size.y - box_height) / 2.0

	var field_left := playable_field.position.x
	var field_right := playable_field.position.x + playable_field.size.x

	# Left penalty box
	draw_line(
		Vector2(field_left, box_y),
		Vector2(field_left + box_depth, box_y),
		line_color,
		line_width
	)
	draw_line(
		Vector2(field_left, box_y + box_height),
		Vector2(field_left + box_depth, box_y + box_height),
		line_color,
		line_width
	)
	draw_line(
		Vector2(field_left + box_depth, box_y),
		Vector2(field_left + box_depth, box_y + box_height),
		line_color,
		line_width
	)

	# Right penalty box
	draw_line(
		Vector2(field_right - box_depth, box_y),
		Vector2(field_right, box_y),
		line_color,
		line_width
	)
	draw_line(
		Vector2(field_right - box_depth, box_y + box_height),
		Vector2(field_right, box_y + box_height),
		line_color,
		line_width
	)
	draw_line(
		Vector2(field_right - box_depth, box_y),
		Vector2(field_right - box_depth, box_y + box_height),
		line_color,
		line_width
	)

func _draw_crowd_row(y: float, total_width: float, left_margin_ratio: float, right_margin_ratio: float, radius: float, spacing: float) -> void:
	var start_x := total_width * left_margin_ratio
	var end_x := total_width * (1.0 - right_margin_ratio)
	var usable_width := end_x - start_x

	if usable_width <= 0.0:
		return

	var count := int(usable_width / spacing)
	for i in range(count):
		var x := start_x + i * spacing
		var color := _pick_crowd_color(i)
		draw_circle(Vector2(x, y), radius, color)

func _pick_crowd_color(index: int) -> Color:
	match index % 3:
		0:
			return crowd_color_a
		1:
			return crowd_color_b
		_:
			return crowd_color_c
