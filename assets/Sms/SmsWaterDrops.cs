using Godot;
using System;

/// <summary>
/// FLUDD's water, after TModelWaterManager (ModelWaterManager.cpp): a pool of drops the nozzles request
/// (emitRequest / makeEmit), moved once per 120 Hz tick like the game's move() -- gravity by particle type, a
/// horizontal speed cap of one drop-size a tick, a lifetime in ticks -- and drawn as one MultiMesh of quads through
/// Fx/water_drop.gdshader, the game's own draw (pass 254): a flying drop is calcDrawVtx's view-space diamond, its front and back
/// corners drawn out along the on-screen velocity x mExtension x 4, wearing drawRefracAndSpec's three passes -- the frame behind it
/// warped by waterref.bti, the water colour (0x3C, 0x46, 0x78) at 0x14, the waterSpec.bti highlight added -- through waterMask.bti; a
/// landed drop is drawWaterVolume's disc: the water_shadow_yuka / kabe volume (radius 11.76 x size x mMagnify x life left) meeting the
/// surface, the water colour at 128 / 255 x 80 / 255, ONE disc a cluster (move() 677-690: a landed drop within 2.5 x (size + size) of an
/// earlier one hands it its size and life and dies -- here that merge is applied to the drawing only, the drops themselves still sit and
/// clean as before). Positions are world metres, velocities metres a tick; the game's numbers (units a tick) are converted on the way in.
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
    public const string BuildMarker = "SMS254_WATER_QUADS_A";

    public float CullDistance = 40f;             // unk5E08 (metres): drops farther than this from Mario in the horizontal are dropped
    public Rid Exclude;                          // Mario's own body
    public uint CollisionMask = uint.MaxValue;
    public Vector3 MarioPos;
    public bool DebugPrint;

    // The look (pass 254): the game's numbers from TModelWaterManager::load (ModelWaterManager.cpp 115-185) and its two volume models; read in _Ready.
    public const string ShaderPath = "res://assets/Sms/Fx/water_drop.gdshader";
    public const string RefTexPath = "res://assets/Sms/Fx/tex/waterref.png";    // /mario/timg/waterref.bti (IA8 32 x 32): the refraction warp
    public const string MaskTexPath = "res://assets/Sms/Fx/tex/watermask.png";  // /mario/timg/waterMask.bti (I8 32 x 32): the drop's coverage
    public const string SpecTexPath = "res://assets/Sms/Fx/tex/waterspec.png";  // /mario/timg/waterSpec.bti (I8 32 x 32): the highlight
    public Color WaterColor = new Color(0x3C / 255f, 0x46 / 255f, 0x78 / 255f, 0x14 / 255f); // gModelWaterManagerWaterColor[0]: the tint and its alpha (raw 8-bit)
    public Color SpecBase = new Color(0x8e / 255f, 0x8e / 255f, 0x9e / 255f);               // unk5D24: the highlight's floor colour
    public Color SpecGain = new Color(0xbc / 255f, 0xcc / 255f, 0xdc / 255f);               // unk5D20: the highlight's colour x waterSpec
    public float RefractScale = 0.07f;           // unk5D1C: screen texels (of the 320 x 224 copy) per 8-bit step of the warp texture
    public float RefractAlpha = 1f;              // unk5D65 / 255: the refraction pass's alpha before the mask
    public float Tail = 4f;                      // unk5D18: how far the quad's front / back corners reach along the on-screen velocity (x mExtension)
    public float PuddleAlpha = 128f / 255f * 80f / 255f;   // drawWaterVolume: dst alpha unk5D5C (128) where the volume meets the ground, x unk5D5D (80) / 255
    public float PuddleRadius = 11.76f;          // water_shadow_yuka.bmd: a ball of radius 11.76 (x 9.6 tall) per unit of the volume's scale (size x mMagnify x life left)
    public float PuddleWallHalfWidth = 8.5f;     // water_shadow_kabe.bmd: x / z +-8.5 ...
    public float PuddleWallUp = 7.29f;           // ... y up to 7.29 ...
    public float PuddleWallDown = 14.1f;         // ... and down to -14.1 (the run down the wall)
    public float PuddleLift = 2f;                // calcVMMtxGround: the volume sits 2 units off the surface along its normal
    public float MergeReach = 2.5f;              // unk5D88[6]: a landed drop within this x (size_a + size_b) of an earlier one is the same puddle
    public float ExtraCullMargin = 8f;           // the quads reach past the drops (a streak's tail, a puddle's 3 m disc): widen the MultiMesh's cull box

    private readonly Vector3[] _pos = new Vector3[MaxDrops];    // world metres
    private readonly Vector3[] _vel = new Vector3[MaxDrops];    // metres a tick
    private readonly float[] _size = new float[MaxDrops];       // metres (mSize / 74.44)
    private readonly float[] _life = new float[MaxDrops];       // ticks left
    private readonly byte[] _type = new byte[MaxDrops];         // the water particle type (WTP index)
    private readonly byte[] _state = new byte[MaxDrops];        // 1 flying, 2 landed
    private readonly Vector3[] _hit = new Vector3[MaxDrops];    // where a landed drop touched the map (the clean stamp's centre)
    private readonly Vector3[] _normal = new Vector3[MaxDrops]; // the surface normal there (the puddle's plane)
    private readonly bool[] _wall = new bool[MaxDrops];         // landed on a wall (splashWall's x 32) rather than a floor (splashGround's x 10)
    private int _count;
    private MultiMeshInstance3D _mmi;
    private MultiMesh _mm;
    private ShaderMaterial _mat;
    private readonly Random _rng = new Random(0x5A17);
    private int _dbgHits;
    // the drawn puddles, one a cluster of landed drops (rebuilt every frame): where, the plane, the wall flag, the volume's size and life
    private readonly Vector3[] _pdHit = new Vector3[MaxDrops];
    private readonly Vector3[] _pdNormal = new Vector3[MaxDrops];
    private readonly bool[] _pdWall = new bool[MaxDrops];
    private readonly float[] _pdSize = new float[MaxDrops];
    private readonly float[] _pdLife = new float[MaxDrops];
    private int _pdCount;

    public int Count => _count;
    /// <summary>Puddles drawn last frame (one a cluster of landed drops).</summary>
    public int PuddleCount => _pdCount;
    public ShaderMaterial Material => _mat;

    public override void _Ready()
    {
        TopLevel = true; // world space, whatever it is parented to
        GlobalTransform = Transform3D.Identity;
        // the quad: TDLTexQuad's four corners with DLUtil.cpp's uv table (0,0) (1,0) (1,1) (0,1); the shader moves a flying drop's corners itself
        // and reads the corner from the uv, a landed drop's through the instance basis (a 1 x 1 square in XY, its normal +Z)
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[] { new(-0.5f, 0.5f, 0f), new(0.5f, 0.5f, 0f), new(0.5f, -0.5f, 0f), new(-0.5f, -0.5f, 0f) };
        arrays[(int)Mesh.ArrayType.Normal] = new Vector3[] { Vector3.Back, Vector3.Back, Vector3.Back, Vector3.Back };
        arrays[(int)Mesh.ArrayType.TexUV] = new Vector2[] { new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f) };
        arrays[(int)Mesh.ArrayType.Index] = new int[] { 0, 1, 2, 0, 2, 3 };
        var quad = new ArrayMesh();
        quad.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,       // INSTANCE_CUSTOM: a flying drop's velocity x mExtension (xyz, metres a tick) and size (w); w = -1 marks a puddle
            Mesh = quad,
            InstanceCount = MaxDrops,
            VisibleInstanceCount = 0,
        };
        var shader = GD.Load<Shader>(ShaderPath);
        if (shader == null)
            GD.PushError($"SmsWaterDrops: no shader at {ShaderPath}");
        _mat = new ShaderMaterial { ResourceName = "sms water drop", Shader = shader };
        _mat.SetShaderParameter("ref_tex", LoadTex(RefTexPath));
        _mat.SetShaderParameter("mask_tex", LoadTex(MaskTexPath));
        _mat.SetShaderParameter("spec_tex", LoadTex(SpecTexPath));
        _mat.SetShaderParameter("water_color", WaterColor);
        _mat.SetShaderParameter("spec_base", new Vector3(SpecBase.R, SpecBase.G, SpecBase.B));
        _mat.SetShaderParameter("spec_gain", new Vector3(SpecGain.R, SpecGain.G, SpecGain.B));
        _mat.SetShaderParameter("ind_scale", RefractScale);
        _mat.SetShaderParameter("refract_alpha", RefractAlpha);
        _mat.SetShaderParameter("tail", Tail);
        _mat.SetShaderParameter("still_len", SmsUnits.UnitsToMetres(1f));
        _mat.SetShaderParameter("puddle_alpha", PuddleAlpha);
        _mmi = new MultiMeshInstance3D
        {
            Name = "SmsWaterDropsMesh",
            Multimesh = _mm,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = _mat,
            ExtraCullMargin = ExtraCullMargin,
        };
        AddChild(_mmi);
    }

    private static Texture2D LoadTex(string path)
    {
        var t = GD.Load<Texture2D>(path);
        if (t == null)
            GD.PushError($"SmsWaterDrops: no texture at {path}");
        return t;
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
                    _normal[i] = n;
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
                _hit[w] = _hit[i]; _normal[w] = _normal[i]; _wall[w] = _wall[i];
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
        _pdCount = 0;
        if (_mm != null)
            _mm.VisibleInstanceCount = 0;
    }

    /// <summary>The frame's instances: the puddles first (one a cluster of landed drops, the shader's puddle mode), then every flying drop
    /// as the game's quad -- its transform the drop's place, its custom data the velocity x mExtension and the size the shader builds
    /// calcDrawVtx's corners from.</summary>
    public override void _Process(double delta)
    {
        if (_mm == null)
            return;
        int n = 0;
        // drawWaterVolume's discs: the volume's scale f = size x mMagnify (move() 539: the size is multiplied by mMagnify as the drop lands)
        // x the life left (calcVMAll: (1 - unk5D88[5] = 0) x life / unk5D68 + 0 -> straight to nothing); a floor disc of radius 11.76 f in the
        // surface's plane, a wall patch 8.5 f each side, 7.29 f up and 14.1 f down (water_shadow_kabe), each 2 units off the surface. One disc a
        // cluster: a landed drop within MergeReach x (f_a + f_b) of an earlier puddle hands it its size and life (move() 677-690) instead of
        // drawing its own.
        _pdCount = 0;
        for (int i = 0; i < _count; i++)
        {
            if (_state[i] != 2)
                continue;
            float f = _size[i] * SmsParams.Fludd.WaterOf(_type[i]).Magnify;
            int at = -1;
            for (int p = 0; p < _pdCount; p++)
            {
                if (_pdWall[p] == _wall[i] && _hit[i].DistanceTo(_pdHit[p]) < MergeReach * (f + _pdSize[p]))
                {
                    at = p;
                    break;
                }
            }
            if (at >= 0)
            {
                _pdSize[at] = f;
                _pdLife[at] = _life[i];
            }
            else
            {
                _pdHit[_pdCount] = _hit[i]; _pdNormal[_pdCount] = _normal[i]; _pdWall[_pdCount] = _wall[i]; _pdSize[_pdCount] = f; _pdLife[_pdCount] = _life[i];
                _pdCount++;
            }
        }
        float lift = SmsUnits.UnitsToMetres(PuddleLift);
        for (int p = 0; p < _pdCount; p++)
        {
            float k = Mathf.Clamp(_pdLife[p] / GroundLifeTicks, 0f, 1f);
            float f = _pdSize[p] * k;
            Vector3 nrm = _pdNormal[p];
            Vector3 refv = MathF.Abs(nrm.Y) > 0.9f ? Vector3.Right : Vector3.Up;
            Vector3 ax = refv.Cross(nrm).Normalized();      // across the surface; on a wall: along it, horizontal
            Vector3 ay = nrm.Cross(ax);                     // on a wall: up the wall
            Vector3 centre = _pdHit[p] + nrm * lift;
            Basis b;
            if (_pdWall[p])
            {
                float w = 2f * PuddleWallHalfWidth * f, h = (PuddleWallUp + PuddleWallDown) * f;
                centre += ay * ((PuddleWallUp - PuddleWallDown) * 0.5f * f);
                b = new Basis(ax * w, ay * h, nrm);
            }
            else
            {
                float w = 2f * PuddleRadius * f;
                b = new Basis(ax * w, ay * w, nrm);
            }
            _mm.SetInstanceTransform(n, new Transform3D(b, centre));
            _mm.SetInstanceCustomData(n, new Color(0f, 0f, 0f, -1f));
            n++;
        }
        // calcDrawVtx: the flying drops, the quad's corners built in the shader from the view-space velocity x mExtension and the size
        for (int i = 0; i < _count; i++)
        {
            if (_state[i] != 1)
                continue;
            Vector3 v = _vel[i] * SmsParams.Fludd.WaterOf(_type[i]).Extension;
            _mm.SetInstanceTransform(n, new Transform3D(Basis.Identity, _pos[i]));
            _mm.SetInstanceCustomData(n, new Color(v.X, v.Y, v.Z, _size[i]));
            n++;
        }
        _mm.VisibleInstanceCount = n;
    }
}
