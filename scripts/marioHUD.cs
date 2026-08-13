using System;
using Godot;

public partial class marioHUD : Node2D
{
    // Called when the node enters the scene tree for the first time.
    Label marioState;
    Label currentAnimation;
    Label carryBool;
    Label spinInput;
    Mario player;
    AnimationPlayer animationPlayer;
    AnimationTree animationTree;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        marioState = GetNode<Label>("marioState");
        currentAnimation = GetNode<Label>("currentAnimation");
        carryBool = GetNode<Label>("carryBool");
        spinInput = GetNode<Label>("spinInput");
        player = GetParent().GetNode<Mario>("Mario");
        animationPlayer = GetParent()
            .GetNode<Mario>("Mario")
            .GetNode<AnimationPlayer>("AnimationPlayer");
        animationTree = GetParent().GetNode<Mario>("Mario").GetNode<AnimationTree>("AnimationTree");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        marioState.Text = "Mario's State: " + player.stateOfMario.ToString();
        var sm = animationTree.Get("parameters/BaseSM/playback").As<AnimationNodeStateMachinePlayback>();
        currentAnimation.Text =
            "Current Animation: " + (sm != null ? sm.GetCurrentNode().ToString() : "N/A");
        carryBool.Text = "Carrying: " + player.IsCarrying.ToString();
        spinInput.Text = "Spin Input: " + player.SpinInput.ToString();
    }
}
