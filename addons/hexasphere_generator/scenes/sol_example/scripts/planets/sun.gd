extends Node3D

@onready var sphere: Node3D = $SunHexasphere

func _ready():
	sphere.PlanetGenerated.connect(_on_planet_generated)

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

	var noise_large: float = sin(n.x * 3.5) * cos(n.y * 3.0) + sin(n.z * 4.0) * cos(n.x * 2.5)
	var noise_med: float = sin(n.y * 12.0) * cos(n.z * 10.0) + sin(n.x * 11.0) * cos(n.y * 13.0)
	var noise_small: float = sin(n.z * 38.0) * cos(n.x * 35.0) + sin(n.y * 32.0) * cos(n.z * 36.0)
	var mixed_noise: float = (noise_large * 0.45) + (noise_med * 0.35) + (noise_small * 0.2)
	var base_noise: float = clamp((mixed_noise + 2.0) / 4.0, 0.0, 1.0)

	if is_nan(base_noise):
		base_noise = 0.5

	var hue: float = lerp(0.0, 0.06, base_noise)
	var saturation: float = lerp(1.0, 0.9, base_noise)
	var value: float = lerp(0.4, 1.0, base_noise)

	return Color.from_hsv(hue, saturation, value)
