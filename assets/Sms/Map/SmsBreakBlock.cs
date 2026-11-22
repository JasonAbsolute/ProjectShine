using System;
using Godot;

/// <summary>
/// The Hotel Lobby secret's three breakable blocks. MapObjBlock.cpp supplies each rule: TIceBlock melts by 0.003 scale per water hit and,
/// below 20 percent height, by 0.004 per 120 Hz tick; TBreakHideObj makes a WatermelonBlock break on HIT_MESSAGE_HIP_DROP;
/// TBrickBlock breaks on marioHeadAttack from below. The Watermelon and Brick switch to their game's animated *break model before disappearing.
/// </summary>
public partial class SmsBreakBlock : AnimatableBody3D, ISmsSprayTarget
{
    public enum BreakRule { IceWater, WatermelonHipDrop, BrickHeadHit }

    /// <summary>Selects the decompiled class and its hit rule.</summary>
    [Export] public BreakRule Rule = BreakRule.WatermelonHipDrop;
    /// <summary>TIceBlock::mMeltSpeedWater: scale removed by each accepted FLUDD drop.</summary>
    [Export] public float MeltPerWaterHit = 0.003f;
    /// <summary>TIceBlock::mMeltSpeedAuto: scale removed per 120 Hz movement tick after the low-height threshold.</summary>
    [Export] public float AutoMeltPerTick = 0.004f;
    /// <summary>TIceBlock::mAutoMeltScale: automatic melting begins below this fraction of the placed Y scale.</summary>
    [Export] public float AutoMeltThreshold = 0.2f;
    /// <summary>The game's movement clock. TMarDirector runs map-object control 120 times a second.</summary>
    [Export] public float TicksPerSecond = 120f;
    /// <summary>The whole model, in game units under the scene's 1/74.44 scale.</summary>
    [Export] public Node3D Model;
    /// <summary>The animated WatermelonBlockBreak or BrickBlockBreak model. Ice has no break model.</summary>
    [Export] public Node3D BreakModel;
    /// <summary>The solid shape disabled as soon as the block breaks or finishes melting.</summary>
    [Export] public CollisionShape3D SolidShape;
    /// <summary>The overlap zone used for Mario's hip-drop or rising head hit.</summary>
    [Export] public Area3D HitZone;
    /// <summary>The BCK clip on the break model.</summary>
    [Export] public string BreakAnimation = "";
    /// <summary>Seconds the broken pieces remain. Defaults are 59/60 for Watermelon and 14/60 for Brick from the BCK files.</summary>
    [Export] public float BreakDurationSeconds = 1f;
    /// <summary>Optional stand-in for the game's break particles.</summary>
    [Export] public PackedScene BreakFxScene;
    /// <summary>Optional watermelon/brick break sound or ice-melt sound.</summary>
    [Export] public AudioStreamPlayer3D BreakSound;

    private Basis _restRotation;
    private Vector3 _initialScale;
    private Vector3 _scale;
    private float _tickAccum;
    private float _brokenFor;
    private bool _broken;

    public bool Broken => _broken;
    public Vector3 CurrentScale => _scale;

    public override void _Ready()
    {
        Model ??= GetNodeOrNull<Node3D>("Model");
        BreakModel ??= GetNodeOrNull<Node3D>("BreakModel");
        SolidShape ??= GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        HitZone ??= GetNodeOrNull<Area3D>("HitZone");
        _restRotation = Transform.Basis.Orthonormalized();
        _initialScale = Transform.Basis.Scale;
        _scale = _initialScale;
        if (BreakModel != null) BreakModel.Visible = false;
        if (SolidShape == null) GD.PushError($"[SmsBreakBlock] {GetPath()}: CollisionShape3D is missing");
        if (Rule != BreakRule.IceWater && HitZone == null) GD.PushError($"[SmsBreakBlock] {GetPath()}: HitZone is missing");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || _broken) return;
        if (Rule == BreakRule.IceWater)
        {
            _tickAccum += (float)delta * TicksPerSecond;
            int ticks = (int)_tickAccum;
            _tickAccum -= ticks;
            if (_scale.Y < _initialScale.Y * AutoMeltThreshold)
                for (int i = 0; i < ticks && !_broken; i++) Melt(AutoMeltPerTick, true);
            return;
        }
        if (HitZone == null) return;
        foreach (Node3D body in HitZone.GetOverlappingBodies())
        {
            if (body is not Mario mario) continue;
            var state = mario.stateOfMario;
            if (Rule == BreakRule.WatermelonHipDrop)
            {
                bool hipDrop = state == Mario.MarioState.groundPoundFalling || state == Mario.MarioState.groundPoundLanding;
                if (hipDrop && mario.GlobalPosition.Y >= GlobalPosition.Y) { BreakNow(); return; }
            }
            else if (Rule == BreakRule.BrickHeadHit)
            {
                bool jump = state == Mario.MarioState.singleJump || state == Mario.MarioState.doubleJump || state == Mario.MarioState.tripleJump
                    || state == Mario.MarioState.SpinJump || state == Mario.MarioState.sideFlip || state == Mario.MarioState.backFlip || state == Mario.MarioState.wallJump;
                if (jump && mario.Velocity.Y > 0f && mario.GlobalPosition.Y < GlobalPosition.Y) { BreakNow(); return; }
            }
        }
    }

    public override void _Process(double delta)
    {
        if (!_broken || Engine.IsEditorHint()) return;
        _brokenFor -= (float)delta;
        if (_brokenFor <= 0f) QueueFree();
    }

    /// <summary>TIceBlock::touchWater. Other block kinds return false so the drop continues as in the game.</summary>
    public bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (_broken || Rule != BreakRule.IceWater) return false;
        SmsWaterHitMarks.Emit(this, hitPos);
        Melt(MeltPerWaterHit, false);
        return true;
    }

    private void Melt(float amount, bool automatic)
    {
        _scale.X -= amount;
        _scale.Y = Mathf.Clamp(_scale.Y - amount, 0.01f, _initialScale.Y);
        _scale.Z -= amount;
        ApplyScale();
        // touchWater dies below X=0; the automatic tail dies below X=0.1.
        if ((!automatic && _scale.X < 0f) || (automatic && _scale.X < 0.1f)) BreakNow();
    }

    private void ApplyScale()
    {
        Basis scaled = new Basis(_restRotation.X * _scale.X, _restRotation.Y * _scale.Y, _restRotation.Z * _scale.Z);
        Transform = new Transform3D(scaled, Transform.Origin);
    }

    /// <summary>TBreakHideObj::kill / TBrickBlock::kill: collision off, whole model hidden, break animation and optional effects started.</summary>
    public void BreakNow()
    {
        if (_broken) return;
        _broken = true;
        if (SolidShape != null) SolidShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        if (HitZone != null) HitZone.SetDeferred(Area3D.PropertyName.Monitoring, false);
        if (Model != null) Model.Visible = false;
        if (BreakModel != null)
        {
            BreakModel.Visible = true;
            AnimationPlayer player = FindAnimationPlayer(BreakModel);
            if (player != null && !string.IsNullOrEmpty(BreakAnimation) && player.HasAnimation(BreakAnimation)) player.Play(BreakAnimation);
        }
        if (BreakFxScene != null && GetParent() != null)
        {
            Node3D fx = BreakFxScene.Instantiate<Node3D>();
            fx.TopLevel = true;
            GetParent().AddChild(fx);
            fx.GlobalTransform = GlobalTransform;
            if (fx is GpuParticles3D particles) { particles.OneShot = true; particles.Emitting = true; particles.Finished += particles.QueueFree; }
        }
        if (BreakSound != null && BreakSound.Stream != null) BreakSound.Play();
        _brokenFor = MathF.Max(0.01f, BreakDurationSeconds);
    }

    private static AnimationPlayer FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer player) return player;
        foreach (Node child in node.GetChildren())
        {
            AnimationPlayer found = FindAnimationPlayer(child);
            if (found != null) return found;
        }
        return null;
    }
}
