using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Sunshine's nozzle box: TNozzleBox (MoveBG/Item.cpp 1181-1338, MapObjInit.cpp NozzleBox_data), the crate that holds a spare nozzle.
/// The model is dolpic0/mapobj/nozzlebox.bmd (205.6 units a side, its origin at the base) with nozzlebox.bmt's J_nozzleBox face --
/// the FLUDD logo and four screws on a white panel -- tinted per nozzle through GX_TEVREG1 (load 1272-1320: hover 0000FF, rocket
/// FF0000, turbo 5A5A78): a blue, a red and a grey box. Mario landing on it (HIT_MESSAGE_TRAMPLE from above, not a belly slide)
/// bounces him (HIT_MESSAGE_ATTACK back: the stomp) and breaks the box (receiveMessage 1210): the item is thrown out of the box's front
/// (throwObjToFront: 50 units up the base, along the box's +Z at the level's speed, with the level's vertical speed -- 20 when unset),
/// the crash model nozzleboxcrash.bmd plays its BCK (49 frames), SE 0x3801 plays and the box is dead. When the item is taken every
/// dead box of that nozzle comes back at once (TItemManager::resetNozzleBoxesModel 16) -- so a box can be used again.
/// THE SHUT BOX. A box is "valid" (unk15C) from its level data, or -- a rocket or turbo box -- when the nozzle right is won
/// (TFlagManager::getNozzleRight); a hover box is always valid (loadAfter). Invalid: makeModelInvalid -- anim 3, the model
/// nozzleboxalpha.bmd, the same box drawn translucent in the nozzle's colour at alpha 100 (unk15E.a = 0x64): the HOLOGRAM. It cannot be
/// hit (HIT_FLAG_CANNOT_GET_HIT: no trample, and nothing to stand on -- Mario walks through it), and touching it brings the FLUDD's
/// balloon (touchPlayer: balloon.bmg 0x59 "This is one of my nozzles, the... Oh, you cannot use it yet. It is only a hologram.") once
/// per contact (unk166, re-armed when he leaves -- control()). A hover box while he holds no nozzle right at all shows 0x5A ("This is
/// one of my nozzles, the Gadd Science, Inc., Hover Nozzle!") once. When the right arrives (HIT_MESSAGE_UNK5) makeModelValid: the box
/// is whole again.
/// The scene is models/Nozzles/NozzleBox.tscn: the body's collision box, the stomp zone over the lid, the box model, the crash model
/// and the hologram model (their meshes wearing the scene's materials), the break sound slot, and the item scene thrown for each
/// nozzle -- all nodes and exports, his to edit. Instance it in a level, set Nozzle and Valid; this script only drives it. A tool
/// script for two things: it tints the materials for the nozzle in the editor as in the game, and shows the hologram when Valid is off.
/// </summary>
[Tool]
public partial class SmsNozzleBox : StaticBody3D
{
    public enum BoxNozzle { Hover = 1, Rocket = 2, Turbo = 3 } // the port's SmsNozzle indices

    private BoxNozzle _nozzle = BoxNozzle.Hover;
    [Export] public BoxNozzle Nozzle
    {
        get => _nozzle;
        set
        {
            _nozzle = value;
            if (IsInsideTree())
                ApplyTint();
        }
    }
    private bool _valid = true;
    /// <summary>unk15C, the level data's "valid": off, the box is the hologram until the nozzle right is won (GrantRight). A hover box is
    /// always valid in the game.</summary>
    [Export] public bool Valid
    {
        get => _valid;
        set
        {
            _valid = value;
            if (IsInsideTree())
                ApplyValid();
        }
    }
    /// <summary>The scene's parts: the box model, the crash model (nozzleboxcrash, its BCK), the hologram model (nozzleboxalpha), the
    /// body's collision, the stomp zone, the break sound (SE 0x3801, its stream his to fill).</summary>
    [Export] public Node3D Model;
    [Export] public Node3D Crash;
    [Export] public Node3D Hologram;
    [Export] public CollisionShape3D Shape;
    [Export] public Area3D StompZone;
    [Export] public AudioStreamPlayer3D BreakSound;
    /// <summary>The item scene thrown out of the box for each nozzle (its root an SmsNozzleItem).</summary>
    [Export] public PackedScene HoverItem;
    [Export] public PackedScene RocketItem;
    [Export] public PackedScene TurboItem;
    /// <summary>throwObjToFront's speed along the box's front, units a 30 fps frame (the level's parameter x 0.02 in the game).</summary>
    [Export] public float ThrowSpeed = 2.0f;
    /// <summary>throwObjToFront's vertical speed, units a 30 fps frame (20 when the level leaves it unset; 10 arcs a metre or two).</summary>
    [Export] public float ThrowVerticalSpeed = 10.0f;
    /// <summary>The FLUDD's balloon on touching the hologram (balloon.bmg 0x59) and on touching a hover box before any nozzle right (0x5A).</summary>
    [Export(PropertyHint.MultilineText)] public string HologramMessage = "This is one of\nmy nozzles,\nthe... Oh, you\ncannot use it\nyet. It is only\na hologram.";
    [Export(PropertyHint.MultilineText)] public string HoverMessage = "This is one of\nmy nozzles,\nthe Gadd\nScience, Inc.,\nHover Nozzle!";

    public const float SizeUnits = 205.6f;
    /// <summary>The hologram's alpha: unk15E.a = 0x64.</summary>
    public const float HologramAlpha = 100f / 255f;

    /// <summary>TFlagManager's nozzle rights (getNozzleRight(map, 0) = the rocket, (map, 1) = the turbo): won when a course grants the
    /// nozzle. The game keeps them per map; here one set for the run -- his progression code calls <see cref="GrantRight"/>.</summary>
    public static readonly HashSet<BoxNozzle> Rights = new();
    public static bool HasAnyRight => Rights.Count > 0;

    private Node3D _model, _crash, _holo;
    private AnimationPlayer _crashAnim;
    private CollisionShape3D _shape;
    private Area3D _stomp;
    private bool _broken;
    private float _respawnScale = 1f;
    private bool _balloonShown; // unk166

    public static Color TintFor(BoxNozzle n) => n switch
    {
        BoxNozzle.Rocket => new Color(1f, 0f, 0f),
        BoxNozzle.Turbo => new Color(0x5A / 255f, 0x5A / 255f, 0x78 / 255f),
        _ => new Color(0f, 0f, 1f),
    };

    public override void _Ready()
    {
        ApplyTint();
        if (Engine.IsEditorHint())
        {
            ApplyValid();
            return;
        }
        _model = Model;
        _crash = Crash;
        _holo = Hologram;
        _shape = Shape;
        _stomp = StompZone;
        if (_model == null || _shape == null || _stomp == null)
            GD.PushWarning($"[SMS nozzle box] {GetPath()}: the scene's Model / Shape / StompZone exports are not all set (see models/Nozzles/NozzleBox.tscn)");
        if (_holo == null)
            GD.PushWarning($"[SMS nozzle box] {GetPath()}: no Hologram model in the scene -- an invalid box has nothing to show (see models/Nozzles/NozzleBox.tscn)");
        _crashAnim = _crash == null ? null : SmsFluddModel.FindChild<AnimationPlayer>(_crash);
        // load / loadAfter: a hover box is always valid; a rocket / turbo box is valid when its right is held
        if (_nozzle == BoxNozzle.Hover || Rights.Contains(_nozzle))
            _valid = true;
        ApplyValid();
    }

    /// <summary>GX_TEVREG1: the nozzle's colour into the face material (NozzleBoxFace.gdshader: the panel stays white, the logo, the
    /// screws and the frame take the colour) on the box and the crash, and into the hologram's material (keeping its alpha) -- one
    /// material each in the scene, local to each instance, so every box has its own.</summary>
    private void ApplyTint()
    {
        Color tint = TintFor(_nozzle);
        foreach (var root in new[] { Model, Crash, Hologram })
        {
            if (root == null)
                continue;
            var meshes = new List<MeshInstance3D>();
            SmsFluddModel.Collect(root, meshes);
            foreach (var mi in meshes)
                for (int i = 0; i < mi.GetSurfaceOverrideMaterialCount(); i++)
                {
                    if (mi.GetSurfaceOverrideMaterial(i) is ShaderMaterial sm)
                        sm.SetShaderParameter("tint", tint);
                    else if (mi.GetSurfaceOverrideMaterial(i) is StandardMaterial3D m)
                        m.AlbedoColor = new Color(tint.R, tint.G, tint.B, root == Hologram ? HologramAlpha : m.AlbedoColor.A);
                }
        }
    }

    /// <summary>makeModelValid / makeModelInvalid: the box or the hologram; the hologram cannot be hit and is not solid.</summary>
    private void ApplyValid()
    {
        if (Model != null) Model.Visible = _valid && !_broken;
        if (Hologram != null) Hologram.Visible = !_valid;
        if (Engine.IsEditorHint())
            return;
        if (Shape != null) Shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, !_valid || _broken);
        if (StompZone != null) StompZone.SetDeferred(Area3D.PropertyName.Monitoring, _valid && !_broken);
    }

    /// <summary>HIT_MESSAGE_UNK5 -> makeModelValid: the right is his, the box is whole.</summary>
    public void SetValid(bool valid)
    {
        if (_valid == valid)
            return;
        _valid = valid;
        ApplyValid();
    }

    /// <summary>The nozzle right won (TFlagManager::setNozzleRight): every box of that nozzle in the tree becomes valid.</summary>
    public static void GrantRight(Node anyNode, BoxNozzle nozzle)
    {
        Rights.Add(nozzle);
        foreach (Node n in anyNode.GetTree().GetNodesInGroup("sms_nozzle_box"))
            if (n is SmsNozzleBox b && b.Nozzle == nozzle)
                b.SetValid(true);
    }

    private bool _respawnPending;

    private bool MarioInside(Mario m) => MarioWithin(m, 0.4f);

    /// <summary>The touch (TMapObjHitInfo 200 x 200: his hit actor against the box's) -- within reach of any face.</summary>
    private bool MarioNear(Mario m) => MarioWithin(m, 1.0f);

    private bool MarioWithin(Mario m, float margin)
    {
        float half = SizeUnits / SmsUnits.UnitsPerMetre * 0.5f + margin;
        Vector3 d = m.GlobalPosition - GlobalPosition;
        return MathF.Abs(d.X) < half && MathF.Abs(d.Z) < half && d.Y > -0.5f && d.Y < SizeUnits / SmsUnits.UnitsPerMetre + 0.5f;
    }

    public string SmsDebug() => $"valid {_valid}, broken {_broken}, pending {_respawnPending}, balloon shown {_balloonShown}, zone monitoring {_stomp?.Monitoring}, overlaps {(_stomp != null && _stomp.Monitoring ? _stomp.GetOverlappingBodies().Count : -1)}";

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint())
            return;
        if (_respawnPending)
        {
            Respawn();
            return;
        }
        // touchPlayer / control(): the FLUDD's balloons
        if (GetTree().GetFirstNodeInGroup("player") is Mario near)
        {
            bool touching = MarioNear(near);
            if (touching && !_balloonShown)
            {
                if (_nozzle == BoxNozzle.Hover && !HasAnyRight)
                {
                    _balloonShown = true;
                    SmsHudLayer.Current?.ShowBalloon(HoverMessage);
                }
                else if (!_valid)
                {
                    _balloonShown = true;
                    SmsHudLayer.Current?.ShowBalloon(HologramMessage);
                }
            }
            if (_nozzle != BoxNozzle.Hover && _balloonShown && !touching)
                _balloonShown = false; // control(): mColCount == 0 re-arms it (the hover box's stays shown)
        }
        if (!_valid || _broken || _stomp == null || !_stomp.Monitoring)
            return;
        // polled, not body_entered: a jump from beside the box puts him in the zone while he still rises, and the landing that follows
        // would be no new entry. His velocity BEFORE this frame's MoveAndSlide is the fall the game's TRAMPLE sees (the landing on the
        // lid has zeroed the body's own); -2.5 m/s keeps a standing Mario (the ground hold's -2) from re-opening a box that comes back
        // under him. Not a belly slide / dive (SMS_IsMarioHeadSlideAttack).
        foreach (Node3D body in _stomp.GetOverlappingBodies())
        {
            if (body is Mario mario && mario.SmsPreMoveVelocity.Y < -2.5f && mario.SmsCanTrampleBox())
            {
                Break(mario);
                break;
            }
        }
    }

    /// <summary>receiveMessage(TRAMPLE): the stomp back to Mario, the item out of the front, the crash BCK, SE 0x3801, the box dead.</summary>
    private void Break(Mario mario)
    {
        _broken = true;
        mario.StompBounce();
        _shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        _stomp.SetDeferred(Area3D.PropertyName.Monitoring, false);
        if (_model != null) _model.Visible = false;
        if (_crash != null)
        {
            _crash.Visible = true;
            if (_crashAnim != null && _crashAnim.HasAnimation("nozzleboxcrash"))
            {
                _crashAnim.Stop();
                _crashAnim.Play("nozzleboxcrash");
                _crashAnim.AnimationFinished += OnCrashFinished;
            }
            else
                _crash.Visible = false;
        }
        if (BreakSound != null && BreakSound.Stream != null)
            BreakSound.Play();
        _debug = mario.SmsDebugPrint;
        CallDeferred(MethodName.SpawnItem); // a new body during the area's own signal: after the physics step
    }

    private bool _debug;

    /// <summary>throwObjToFront(item, 50, speed, vertical): from 50 units up the base, along the box's front (its +Z), the level's speeds.</summary>
    private void SpawnItem()
    {
        var scene = Nozzle switch { BoxNozzle.Rocket => RocketItem, BoxNozzle.Turbo => TurboItem, _ => HoverItem };
        if (scene?.Instantiate() is not SmsNozzleItem item)
        {
            GD.PushWarning($"[SMS nozzle box] {GetPath()}: no {Nozzle}Item scene with an SmsNozzleItem root -- the box breaks and throws nothing (set it in NozzleBox.tscn)");
            return;
        }
        item.Nozzle = (SmsNozzleItem.SmsNozzleKind)(int)Nozzle;
        item.Box = this;
        GetParent().AddChild(item);
        float s = 1f / SmsUnits.UnitsPerMetre;
        item.GlobalPosition = GlobalPosition + new Vector3(0f, 50f * s, 0f);
        Vector3 front = GlobalTransform.Basis.Z.Normalized();
        item.SetVelocityUnitsPerFrame(front * ThrowSpeed + Vector3.Up * ThrowVerticalSpeed);
        if (_debug)
            GD.Print($"[SMS nozzle box] {Nozzle} box trampled: the item out along {front} at {ThrowSpeed} u/frame, up {ThrowVerticalSpeed}; the crash plays");
    }

    private void OnCrashFinished(StringName anim)
    {
        if (_crashAnim != null)
            _crashAnim.AnimationFinished -= OnCrashFinished;
        if (_crash != null)
            _crash.Visible = false; // makeObjDead
    }

    /// <summary>resetNozzleBoxesModel: the item of this nozzle was taken -- every dead box of the kind is whole again.</summary>
    public void Respawn()
    {
        if (!_broken)
            return;
        // never back onto Mario: the game's boxes respawn where the item lands clear of them; here the box waits until he has stepped out
        if (GetTree().GetFirstNodeInGroup("player") is Mario m && MarioInside(m))
        {
            _respawnPending = true;
            return;
        }
        _respawnPending = false;
        _broken = false;
        if (_crashAnim != null && _crashAnim.IsPlaying())
        {
            _crashAnim.AnimationFinished -= OnCrashFinished;
            _crashAnim.Stop();
        }
        if (_crash != null) _crash.Visible = false;
        ApplyValid();
        _respawnScale = 0.3f; // the smoke puff's stand-in: a short scale-in
    }

    public override void _Process(double delta)
    {
        if (_respawnScale < 1f && _model != null)
        {
            _respawnScale = MathF.Min(1f, _respawnScale + (float)delta * 5f);
            float s = _respawnScale / SmsUnits.UnitsPerMetre;
            _model.Scale = new Vector3(s, s, s);
        }
    }

    /// <summary>Every box of a nozzle in the tree (the item manager's loop over the boxes).</summary>
    public static void RespawnAll(Node anyNode, BoxNozzle nozzle)
    {
        foreach (Node n in anyNode.GetTree().GetNodesInGroup("sms_nozzle_box"))
            if (n is SmsNozzleBox b && b.Nozzle == nozzle)
                b.Respawn();
    }

    public override void _EnterTree()
    {
        if (!Engine.IsEditorHint())
            AddToGroup("sms_nozzle_box");
    }
}
