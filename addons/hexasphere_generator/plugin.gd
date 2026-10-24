@tool
extends EditorPlugin

func _enter_tree() -> void:
	# Read the source image directly: a fresh project may not have imported it yet.
	var icon_image := Image.load_from_file("res://addons/hexasphere_generator/icon.svg")
	var icon := ImageTexture.create_from_image(icon_image)
	add_custom_type(
		"Hexasphere",            
		"Node3D",                          
		preload("res://addons/hexasphere_generator/scripts/hexasphere_node/HexasphereNode.cs"),
		icon
	)
	
	add_custom_type(
		"HexasphereUVProjector",            
		"Node2D",                          
		preload("res://addons/hexasphere_generator/scripts/hexasphere_uv_projector/HexasphereProjectorController.cs"),
		icon
	)
	

func _exit_tree() -> void:
	remove_custom_type("Hexasphere")
	remove_custom_type("HexasphereUVProjector")
