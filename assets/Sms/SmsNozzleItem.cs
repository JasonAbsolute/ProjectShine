using Godot;
using System;

/// <summary>
/// The nozzle item a nozzle box throws out: TItemNozzle (MoveBG/Item.cpp 1099-1180) on the map-object physics of MapObjInit.cpp's
/// nozzle_normal_physical_data { gravity 0.35, bounce 0.5, -, stop below 5, landing friction 0.8, -, air drag 1.0 } -- units a 30 fps
/// frame (TMapObjGeneral::calcVelocity 472, touchGround 434). It spins with every item at 2 degrees a frame (TItemManager::perform
/// 112). Its model is dolpic0/mapobj/{normal,rocket,back}_nozzle_item.bmd on the FLUDD atlas (nozzleitem.bmt: H_watergun_main).
/// Mario touching it (touchPlayer 1099: a 40 x 100 unit cylinder, nozzle_item_hit_data_table) gives him the nozzle -- TMario::getNozzle
/// (MarioReceiveMsg.cpp 18): changeNozzle(type, animate) with the swap already at 1, the tank refilled, the get sparkle -- and every
/// dead box of that nozzle comes back (resetNozzleBoxesModel). MAP_OBJ_FLAG_UNK10000000 (load 1168) spares it the item timer: it
/// waits until taken.
/// The scenes are models/Nozzles/{Hover,Rocket,Turbo}/*NozzleItem.tscn: the model (Jaysonn's textured exports) and the Hit cylinder
/// as nodes, this script driving them. A box throws the scene its export names; one can also lie in a level on its own.
/// </summary>
public partial class SmsNozzleItem : Node3D
{
    [Export] public SmsNozzleBox.BoxNozzle Nozzle = SmsNozzleBox.BoxNozzle.Hover;
    /// <summary>The scene's parts: the model that spins, the 40 x 100 unit touch cylinder.</summary>
    [Export] public Node3D Model;
    [Export] public Area3D Hit;
    /// <summary>The map-object clock: 1 = the game's 30 fps frames (a floaty two-second arc on gravity 0.35), 3 = Jaysonn's -- a
    /// logged deviation, like the HUD's pace: "the item coming out of the box is slow just like the hud menu was".</summary>
    [Export] public float Speed = 3f;
    public SmsNozzleBox Box;

    private const float Gravity = 0.35f, Bounce = 0.5f, StopBelow = 5f, LandFriction = 0.8f, SpinDegPerFrame = 2f;
    private const float BodyRadiusUnits = 40f; // mBodyRadius: the fall speed's clamp

    private Vector3 _velUnits;      // units a frame
    private bool _airborne = true;
    private float _frameAcc;
    private Node3D _model;
    private Area3D _hit;
    private bool _taken;

    public void SetVelocityUnitsPerFrame(Vector3 v) => _velUnits = v;

    public override void _Ready()
    {
        _model = Model;
        _hit = Hit;
        if (_model == null || _hit == null)
            GD.PushWarning($"[SMS nozzle item] {GetPath()}: the scene's Model / Hit exports are not both set (see models/Nozzles/*/*NozzleItem.tscn)");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_taken)
            return;
        _frameAcc += (float)delta * 30f * Speed;
        int frames = (int)MathF.Floor(_frameAcc);
        _frameAcc -= frames;
        for (int i = 0; i < frames && !_taken; i++)
            Frame();
        if (_model != null)
            _model.RotationDegrees = new Vector3(0f, _model.RotationDegrees.Y + SpinDegPerFrame * 30f * Speed * (float)delta, 0f);
        // TItem::appear sets HIT_FLAG_NO_COLLISION; TItemNozzle::appearing drops it once LIVE_FLAG_UNK10 -- the stop on the ground --
        // is up: the item cannot be taken in flight (nor inside the breaking box). Polled: he may already stand where it lands.
        if (!_airborne && !_taken && _hit != null)
            foreach (Node3D body in _hit.GetOverlappingBodies())
                if (body is Mario mario)
                {
                    Take(mario);
                    break;
                }
    }

    /// <summary>One map-object frame: calcVelocity's gravity, the move, checkGroundCollision / touchGround's bounce or stop.</summary>
    private void Frame()
    {
        float s = 1f / SmsUnits.UnitsPerMetre;
        if (_airborne)
        {
            _velUnits.Y -= Gravity;
            _velUnits.Y = Mathf.Clamp(_velUnits.Y, -BodyRadiusUnits, BodyRadiusUnits); // clamped to +-mBodyRadius
        }
        else if (_velUnits.LengthSquared() < 1e-6f)
            return;
        Vector3 from = GlobalPosition;
        Vector3 to = from + _velUnits * s;
        // the ground under the new spot (checkGroundCollision: the ground at the head height, +1)
        var space = GetWorld3D().DirectSpaceState;
        var q = PhysicsRayQueryParameters3D.Create(new Vector3(to.X, MathF.Max(from.Y, to.Y) + 0.4f, to.Z), new Vector3(to.X, to.Y - 0.05f, to.Z), 1);
        var hit = space.IntersectRay(q);
        if (hit.Count > 0)
        {
            float groundY = ((Vector3)hit["position"]).Y + 1f * s;
            if (to.Y <= groundY)
            {
                // touchGround: the horizontal speed x 0.8; a fall faster than 5 bounces at half, slower stops
                _velUnits.X *= LandFriction;
                _velUnits.Z *= LandFriction;
                if (MathF.Abs(_velUnits.Y) > StopBelow)
                {
                    to.Y -= _velUnits.Y * s; // param_1->y -= velocity.y
                    _velUnits.Y *= -Bounce;
                }
                else
                {
                    to.Y = groundY;
                    _velUnits = Vector3.Zero;
                    _airborne = false;
                }
            }
            else
                _airborne = true;
        }
        else
            _airborne = true;
        GlobalPosition = to;
        if (to.Y < -200f)
            QueueFree(); // fell out of the world
    }

    /// <summary>touchPlayer -> taken: the nozzle to Mario, the boxes of this nozzle back, the item gone.</summary>
    private void Take(Mario mario)
    {
        _taken = true;
        mario.SmsGetNozzle((int)Nozzle);
        SmsNozzleBox.RespawnAll(this, Nozzle); // resetNozzleBoxesModel(kind)
        QueueFree();
    }
}
