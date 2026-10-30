using Godot;
using System;

/// <summary>
/// FLUDD's water, after TModelWaterManager (ModelWaterManager.cpp): a pool of drops the nozzles request
/// (emitRequest / makeEmit), moved once per 120 Hz tick like the game's move() -- gravity by particle type, a
/// horizontal speed cap of one drop-size a tick, a lifetime in ticks -- and drawn as one MultiMesh of stretched
/// spheres (a stand-in for the game's silhouette / water-volume rendering). Positions are world metres, velocities
/// metres a tick; the game's numbers (units a tick) are converted on the way in.
/// Not ported: the ground / wall sliding (case 2 / 3: a hit drop sits, shrinks and dries instead), splashes, sound. A drop whose
/// ray hits a body that is an <see cref="ISmsSprayTarget"/> (the enemies: Gooble.cs; the NPCs: SmsNpc.cs) sends it HIT_MESSAGE_SPRAYED_BY_WATER
/// (pass 71; the game's water hit actors against the actors' damage cylinders) and, taken, dies there (pass 99, move() 429). A drop that lands cleans the goop layers (splashGround / splashWall ->
/// gpPollution->clean(pos, mCleanSize x 10 on a floor, x 32 on a wall)) the tick it lands and every tick it sits there (pass 65).
/// </summary>
/// <summary>An actor FLUDD's water hurts: TSmallEnemy::receiveMessage(HIT_MESSAGE_SPRAYED_BY_WATER) and the like.</summary>
public interface ISmsSprayTarget
{
    /// <summary>A drop (or the hose at close range) hit the actor at <paramref name="hitPos"/> (world metres). True = the actor took the message
    /// (receiveMessage returned true): TModelWaterManager::move() 429 then KILLS the drop -- it neither lands nor cleans. False = it flies on
    /// (here: it lands where it was).</summary>
    bool SmsSprayedByWater(Vector3 hitPos);
}

public partial class SmsWaterDrops : Node3D
{
    public const int MaxDrops = 512;             // the manager's pool size (unk10) is not in the decomp; 255 alive x 0.8 a tick plus a 127-drop burst fits
    public const int GroundLifeTicks = 40;       // stand-in for the sliding / drying of a landed drop (case 2 + getPlaneVanishSpeed): a third of a second

    public float CullDistance = 40f;             // unk5E08 (metres): drops farther than this from Mario in the horizontal are dropped
    public Rid Exclude;                          // Mario's own body
    public uint CollisionMask = uint.MaxValue;
    public Vector3 MarioPos;
    public bool DebugPrint;

    private readonly Vector3[] _pos = new Vector3[MaxDrops];    // world metres
    private readonly Vector3[] _vel = new Vector3[MaxDrops];    // metres a tick
    private readonly float[] _size = new float[MaxDrops];       // metres (mSize / 74.44)
    private readonly float[] _life = new float[MaxDrops];       // ticks left
    private readonly byte[] _type = new byte[MaxDrops];         // the water particle type (WTP index)
    private readonly byte[] _state = new byte[MaxDrops];        // 1 flying, 2 landed
    private readonly Vector3[] _hit = new Vector3[MaxDrops];    // where a landed drop touched the map (the clean stamp's centre)
    private readonly bool[] _wall = new bool[MaxDrops];         // landed on a wall (splashWall's x 32) rather than a floor (splashGround's x 10)
    private int _count;
    private MultiMeshInstance3D _mmi;
    private MultiMesh _mm;
    private readonly Random _rng = new Random(0x5A17);
    private int _dbgHits;

    public int Count => _count;

    public override void _Ready()
    {
        TopLevel = true; // world space, whatever it is parented to
        GlobalTransform = Transform3D.Identity;
        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 10, Rings = 5 },
            InstanceCount = MaxDrops,
            VisibleInstanceCount = 0,
        };
        _mmi = new MultiMeshInstance3D
        {
            Name = "SmsWaterDropsMesh",
            Multimesh = _mm,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ResourceName = "sms water drop",
                AlbedoColor = new Color(0.42f, 0.78f, 1.0f, 0.82f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.12f,
                Metallic = 0.05f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.SchlickGgx,
                EmissionEnabled = true,
                Emission = new Color(0.05f, 0.18f, 0.35f),
                EmissionEnergyMultiplier = 0.6f,
            },
        };
        AddChild(_mmi);
    }

    /// <summary>rand11(): ((rand() & 0xff) - 128) / 128.</summary>
    private float Rand11() => ((_rng.Next() & 0xff) - 128) / 128f;

    /// <summary>emitRequest() + makeEmit(): spawn <paramref name="num"/> drops from <paramref name="pos"/> along <paramref name="dir"/>
    /// (unit vector, world), with the game's tremble / launch-speed / size rules; returns how many were spawned (the tank is
    /// charged per spawned drop). <paramref name="powUnits"/>, <paramref name="powTremble"/> are units a tick,
    /// <paramref name="sizeUnits"/>, <paramref name="sizeTremble"/> units, <paramref name="inheritUnitsPerTick"/> the emitter's
    /// own motion (units a tick).</summary>
    public int Emit(int num, Vector3 pos, Vector3 dir, float dirTremble, float powUnits, float powTremble,
                    float sizeUnits, float sizeTremble, int type, Vector3 inheritUnitsPerTick)
    {
        int spawned = 0;
        var wt = SmsParams.Fludd.WaterOf(type);
        while (num > 0 && _count < MaxDrops)
        {
            int i = _count;
            Vector3 d = dir + new Vector3(Rand11(), Rand11(), Rand11()) * dirTremble;
            if (d.LengthSquared() < 1e-12f)
                d = Vector3.Down;
            d = d.Normalized();
            float speed = Rand11() * powTremble + powUnits;
            _pos[i] = pos;
            _vel[i] = (d * speed + inheritUnitsPerTick) * SmsUnits.ArmatureScale; // units a tick -> metres a tick
            _size[i] = MathF.Max(1f, Rand11() * sizeTremble + sizeUnits) * SmsUnits.ArmatureScale;
            _life[i] = wt.Alive;
            _type[i] = (byte)type;
            _state[i] = 1;
            _count++;
            spawned++;
            num--;
        }
        return spawned;
    }

    /// <summary>move(), one tick: the flying drops get their type's gravity, the one-size-a-tick horizontal cap and the
    /// step; a step that meets geometry lands the drop (it sits and dries); lifetimes count down; the dead are compacted out.</summary>
    public void Tick()
    {
        if (_count == 0)
            return;
        var space = GetWorld3D().DirectSpaceState;
        float cull2 = CullDistance * CullDistance;
        for (int i = 0; i < _count; i++)
        {
            if (_state[i] == 1)
            {
                Vector3 p = _pos[i];
                float dx = p.X - MarioPos.X, dz = p.Z - MarioPos.Z;
                if (dx * dx + dz * dz > cull2)
                {
                    _life[i] = 0f;
                    continue;
                }
                // pass 123b -- TObjHitCheck::checkWater (ObjHitCheck.cpp 97) + move() 424: every flying drop is a 50 x 80 cylinder tested against the
                // actors' damage cylinders; a hidden-coin spot / graffiti picture it overlaps takes the drop (HIT_MESSAGE_SPRAYED_BY_WATER) and the drop
                // dies there. The ray below only meets what the drop's path CROSSES; the game's test also counts a drop passing beside a spot.
                var zone = SmsHiddenCoin.WaterZoneAt(p);
                if (zone != null && zone.SmsSprayedByWater(p)) { _life[i] = 0f; continue; }
                var bud = SmsSamboFlower.ZoneAt(p);      // pass 129: a Pokey's sunflower bud (TSamboFlower's 30 x 120 hit actor) -- no body, the game's cylinder test
                if (bud != null && bud.SmsSprayedByWater(p)) { _life[i] = 0f; continue; }
                Vector3 v = _vel[i];
                float size = _size[i];
                float hor2 = v.X * v.X + v.Z * v.Z;
                if (hor2 > size * size)
                {
                    // a drop never moves more than its own size a tick in the horizontal (no tunnelling)
                    float k = (size - 0.1f * SmsUnits.ArmatureScale) / MathF.Sqrt(hor2);
                    v.X *= k;
                    v.Z *= k;
                }
                v.Y += SmsParams.Fludd.WaterOf(_type[i]).Gravity * SmsUnits.ArmatureScale;
                Vector3 np = p + v;
                var rq = PhysicsRayQueryParameters3D.Create(p, np, CollisionMask);
                rq.CollideWithAreas = false;
                if (Exclude.IsValid)
                    rq.Exclude = new Godot.Collections.Array<Rid> { Exclude };
                var hit = space.IntersectRay(rq);
                if (hit.Count > 0)
                {
                    Vector3 n = (Vector3)hit["normal"];
                    _hit[i] = (Vector3)hit["position"];
                    // move() 429: a drop that meets an actor sends it HIT_MESSAGE_SPRAYED_BY_WATER; taken, the drop DIES there (mParticleLifetimeSOA
                    // = 0: no landing, no clean stamp -- the game adds a splash sound and, 30 % of the time, a TSplashManager splash, neither ported;
                    // the actor's own answer, the ENM_WATHIT mark, is its receiveMessage's). Pass 99 -- before this the drop sat and dried on the body.
                    if (hit["collider"].As<GodotObject>() is ISmsSprayTarget target)
                    {
                        if (target.SmsSprayedByWater(_hit[i])) { _life[i] = 0f; continue; }
                    }
                    else if (DebugPrint && (++_dbgHits % 120) == 1 && hit["collider"].As<GodotObject>() is Node col && col is not StaticBody3D && col is not CsgShape3D)
                        GD.Print($"[SMS water] a drop hit {col.Name} ({col.GetType().Name}): not a spray target");
                    _wall[i] = n.Y < 0.5f; // the game asks the polygon's class (floor / wall); the port splits at 60 degrees
                    _pos[i] = _hit[i] + n * (size * 0.25f);
                    _vel[i] = Vector3.Zero;
                    _state[i] = 2;
                    CleanAt(i);
                    _life[i] = MathF.Min(_life[i], GroundLifeTicks);
                }
                else
                {
                    _pos[i] = np;
                    _vel[i] = v;
                }
            }
            else if (_state[i] == 2)
                CleanAt(i); // splashGround / splashWall run every frame a landed drop lives
            _life[i] -= 1f;
        }
        // garbageCollect()
        int w = 0;
        for (int i = 0; i < _count; i++)
        {
            if (_life[i] <= 0f)
                continue;
            if (w != i)
            {
                _pos[w] = _pos[i]; _vel[w] = _vel[i]; _size[w] = _size[i]; _life[w] = _life[i]; _type[w] = _type[i]; _state[w] = _state[i];
                _hit[w] = _hit[i]; _wall[w] = _wall[i];
            }
            w++;
        }
        _count = w;
    }

    /// <summary>TModelWaterManager::splashGround / splashWall: the drop's water particle type's mCleanSize x 10 (a floor) or x 32 (a wall) as the
    /// clean stamp's width at the spot it touched -- the spray's 18 cleans a 180-unit disc a drop a tick.</summary>
    private void CleanAt(int i)
    {
        float scale = _wall[i] ? SmsParams.Pollution.CleanWallScale : SmsParams.Pollution.CleanGroundScale;
        SmsPollutionMap.Clean(_hit[i], SmsParams.Fludd.WaterOf(_type[i]).CleanSize * scale);
    }

    /// <summary>Everything gone (a respawn).</summary>
    public void Clear()
    {
        _count = 0;
        if (_mm != null)
            _mm.VisibleInstanceCount = 0;
    }

    public override void _Process(double delta)
    {
        if (_mm == null)
            return;
        for (int i = 0; i < _count; i++)
        {
            var wt = SmsParams.Fludd.WaterOf(_type[i]);
            float d = _size[i] * wt.Magnify * 2f; // the drawn diameter: the game scales its blob by Magnify
            Transform3D xf;
            if (_state[i] == 1)
            {
                Vector3 v = _vel[i];
                float vl = v.Length();
                if (vl > 1e-5f)
                {
                    // stretched along the flight so successive drops (one launch-step apart) read as one stream (mExtension)
                    float len = MathF.Max(d, vl * (1f + wt.Extension));
                    Vector3 fwd = v / vl;
                    Vector3 up = MathF.Abs(fwd.Y) > 0.98f ? Vector3.Right : Vector3.Up;
                    Basis look = Basis.LookingAt(fwd, up);
                    xf = new Transform3D(new Basis(look.X * d, look.Y * d, look.Z * len), _pos[i]);
                }
                else
                    xf = new Transform3D(Basis.FromScale(new Vector3(d, d, d)), _pos[i]);
            }
            else
            {
                float k = Mathf.Clamp(_life[i] / GroundLifeTicks, 0f, 1f);
                xf = new Transform3D(Basis.FromScale(new Vector3(d * 1.6f * k, d * 0.3f * k, d * 1.6f * k)), _pos[i]);
            }
            _mm.SetInstanceTransform(i, xf);
        }
        _mm.VisibleInstanceCount = _count;
    }
}
