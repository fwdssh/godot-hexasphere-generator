@tool
extends EditorPlugin

func _enter_tree() -> void:
	add_custom_type(
		"HexasphereDemo",            
		"Node3D",                          
		preload("res://addons/hexasphere_generator/scripts/example/HexasphereExample.cs"),
		preload("res://addons/hexasphere_generator/icon.svg")
	)
	

func _exit_tree() -> void:
	remove_custom_type("HexasphereDemo")
