using Godot;
using System;

public partial class marioHUD : Node2D
{
	// Called when the node enters the scene tree for the first time.
	Label marioState;
	Label currentAnimation;
	Mario player;
	AnimationPlayer animationPlayer;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		marioState = GetNode<Label>("marioState");
		currentAnimation = GetNode<Label>("currentAnimation");
		player = GetParent().GetNode<Mario>("Mario");
		animationPlayer = GetParent().GetNode<Mario>("Mario").GetNode<AnimationPlayer>("AnimationPlayer");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		marioState.Text = "Mario's State: " + player.stateOfMario.ToString();
		currentAnimation.Text = "Current Animation: " + animationPlayer.CurrentAnimation;
	}
}
