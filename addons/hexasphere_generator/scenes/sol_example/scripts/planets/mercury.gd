extends Node3D

@export var orbit_radius: float = 250.0
@export var orbit_speed: float = 0.3

@onready var sphere: Node3D = $MercuryHexasphere
var _orbit_angle: float = 0.0

func _ready():
	sphere.PlanetGenerated.connect(_on_planet_generated)

func _process(delta: float):
	_orbit_angle += orbit_speed * delta

	global_position.x = cos(_orbit_angle) * orbit_radius
	global_position.z = sin(_orbit_angle) * orbit_radius
	global_position.y = 0.0
	# Реалистичный резонанс 3:2 (скорость вращения строго в 1.5 раза быстрее орбитальной)
	sphere.rotate_y(orbit_speed * 1.5 * delta)

func _on_planet_generated(tile_count: int):
	var colors: Array[Color] = []
	colors.resize(tile_count)
	for i in range(tile_count):
		colors[i] = _color_logic(sphere.GetTileCenter(i))
	sphere.SetAllTileColors(colors)

func _color_logic(tile_center: Vector3) -> Color:
	var n := tile_center.normalized()

	if is_nan(n.x) or is_nan(n.y) or is_nan(n.z):
		n = Vector3.UP

	var noise_large: float = sin(n.x * 2.5) * cos(n.y * 2.0) + sin(n.z * 3.0) * cos(n.x * 1.8)
	var noise_med: float = sin(n.y * 10.0) * cos(n.z * 8.0) + sin(n.x * 9.0) * cos(n.y * 11.0)
	var noise_small: float = sin(n.z * 32.0) * cos(n.x * 28.0) + sin(n.y * 26.0) * cos(n.z * 30.0)
	var mixed_noise: float = (noise_large * 0.5) + (noise_med * 0.3) + (noise_small * 0.2)
	var base_noise: float = clamp((mixed_noise + 2.0) / 4.0, 0.0, 1.0)

	if is_nan(base_noise):
		base_noise = 0.5

	var hue: float = 0.08
	var saturation: float = lerp(0.12, 0.04, base_noise)
	var value: float = lerp(0.18, 0.72, base_noise)

	var crater_ray: float = sin(n.x * 45.0 + n.z * 35.0) * cos(n.y * 40.0)
	if crater_ray > 0.93 and base_noise > 0.65:
		saturation = 0.05
		value = 0.8

	return Color.from_hsv(hue, saturation, value)
