extends Node3D

@export var orbit_radius: float = 1000.0
@export var orbit_speed: float = 0.05
@export var rotation_speed: float = 0.8

@onready var sphere: Node3D = $NeptunHexasphere
var _orbit_angle: float = 0.0

func _ready():
	sphere.PlanetGenerated.connect(_on_planet_generated)

func _process(delta: float):
	_orbit_angle += orbit_speed * delta

	global_position.x = cos(_orbit_angle) * orbit_radius
	global_position.z = sin(_orbit_angle) * orbit_radius
	global_position.y = 0.0
	sphere.rotate_y(rotation_speed * delta)

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

	var flow_noise: float = sin(n.x * 4.5) * cos(n.z * 4.0) + sin(n.z * 2.5) * cos(n.y * 2.0)
	var distorted_lat: float = asin(n.y) + flow_noise * 0.18
	var band: float = abs(sin(distorted_lat * 4.5) * cos(distorted_lat * 1.5))

	var cloud_noise_1: float = sin(n.x * 16.0) * cos(n.y * 14.0) + sin(n.z * 12.0) * cos(n.x * 10.0)
	var cloud_noise_2: float = sin(n.y * 38.0) * cos(n.z * 32.0) * sin(n.x * 35.0)
	var mixed_noise: float = (cloud_noise_1 * 0.6 + cloud_noise_2 * 0.4 + 2.0) / 4.0

	var atmos: float = clamp(band * 0.4 + mixed_noise * 0.6, 0.0, 1.0)

	var hue: float = lerp(0.55, 0.61, atmos)
	var saturation: float = lerp(0.88, 0.65, atmos)
	var value: float = lerp(0.22, 0.58, atmos)

	return Color.from_hsv(hue, saturation, value)
