using Godot;
using System;

public partial class marioHUD : Node2D
{
	// Called when the node enters the scene tree for the first time.
	Label Hud;
	Mario player;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Hud = GetNode<Label>("marioState");
		player = GetParent().GetNode<Mario>("Mario");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Hud.Text = player.stateOfMario.ToString();
	}
}
