extends Node3D

@export var orbit_radius: float = 300.0
@export var orbit_speed: float = 0.22
@export var rotation_speed: float = -0.05

@onready var sphere: Node3D = $VenusHexasphere
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

	var latitude := asin(n.y)
	var band := abs(sin(latitude * 3.0))
	var noise := (sin(n.x * 4.0) * cos(n.z * 3.0) + sin(n.z * 5.0) * cos(n.y * 2.0) + 2.0) / 4.0
	var blend := clamp(band * 0.3 + noise * 0.7, 0.0, 1.0)

	var hue := lerp(0.11, 0.14, blend)
	var saturation := lerp(0.28, 0.15, blend)
	var value := lerp(0.82, 0.96, blend)

	return Color.from_hsv(hue, saturation, value)
