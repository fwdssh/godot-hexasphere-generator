extends Node3D

@onready var sphere: Node3D = $Hexasphere

func _ready():
	sphere.color_provider = Callable(self, "_my_tile_color")

func _my_tile_color(cell_data: HexCellData) -> Color:
	return Color(randf(), randf(), randf())
