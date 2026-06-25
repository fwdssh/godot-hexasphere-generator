@tool
extends EditorPlugin

func _enter_tree() -> void:
	add_custom_type(
		"Hexasphere",            
		"Node3D",                          
		preload("res://addons/hexasphere_generator/scripts/hexasphere_node/HexasphereNode.cs"),
		preload("res://addons/hexasphere_generator/icon.svg")
	)
	

func _exit_tree() -> void:
	remove_custom_type("Hexasphere")
