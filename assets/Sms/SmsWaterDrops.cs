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
/// earlier one hands it its size and life and dies -- 263: simulated in Tick, as the game's). Positions are world metres, velocities metres a
/// tick; the game's numbers (units a tick) are converted on the way in.
/// 263: a landed drop LIVES the game's way -- move()'s case 2 / 3 with gWaterManagerPlaneInfo: its life capped at 256 ticks and run down by the plane's
/// vanish speed (1 a tick; 2 on a wet plane), its horizontal speed x the plane's friction (0.7; 0.93 wet) + the plane normal's horizontal part (downhill), its
/// height put back on the floor each tick, a wall drop running down the wall (-1 / -3 units a tick) and dying at the floor, a floor drop that slides off an
/// edge flying again; and the landing's chance to survive as a puddle at all (0.5; 0.35 under the L camera; a wall 0.5). The silhouette pass (drawSilhouette):
/// SetSilhouette, a second MultiMeshInstance3D in water_silhouette.gdshader that Mario's SmsSilhouetteTick drives. Not ported: splashes, sound. A drop whose
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
    /// <summary>263: unk5D68 / unk5D6C -- a drop that lands (a floor or a wall) has its life capped here, and it then runs down by the plane's vanish speed
    /// a tick (splashGround / splashWall): up to 256 ticks (2.1 s) on a plain floor, half that on a wet one. The puddle's disc shrinks with it.</summary>
    public const int LandedLifeMax = 256;
    public const string BuildMarker = "SMS263_WATER_SLIDE_SILHOUETTE";

    public float CullDistance = 40f;             // unk5E08 (metres): drops farther than this from Mario in the horizontal are dropped
    public Rid Exclude;                          // Mario's own body
    public uint CollisionMask = uint.MaxValue;
    public Vector3 MarioPos;
    public bool DebugPrint;
    // 263 -- the landed drops, after move()'s case 2 / 3 and gWaterManagerPlaneInfo (ModelWaterManager.cpp 355: { 0.93, -3.0, 2.0, 0.7, -1.0, 1.0 }):
    /// <summary>A landed drop's horizontal speed each tick is itself x this (the plane's friction) + the plane's normal's horizontal part (one unit a tick
    /// downhill): 0.7 on a plain floor (it stops in a few ticks), 0.93 on a wet one (TBGCheckData::isWaterSlip: the hydrophobic / wet-ground types -- here a
    /// collider in the group <see cref="SlipGroup"/>).</summary>
    public float FrictionNormal = 0.7f, FrictionSlip = 0.93f;
    /// <summary>A drop on a wall runs down it this many units a tick (getPlaneFall): -1 plain, -3 wet.</summary>
    public float FallNormal = -1f, FallSlip = -3f;
    /// <summary>A landed drop's life runs down this many ticks a tick (getPlaneVanishSpeed): 1 plain (256 ticks), 2 wet (128).</summary>
    public float VanishNormal = 1f, VanishSlip = 2f;
    /// <summary>move() 541-549: a drop that meets a floor SURVIVES as a puddle with this chance (unk5D88[0] = 0.5; [1] = 0.35 under the L camera, LCamera),
    /// else it dies in its splash; a wall: unk5D88[2] = 0.5.</summary>
    public float LandSurviveChance = 0.5f, LandSurviveChanceLCamera = 0.35f, WallSurviveChance = 0.5f;
    /// <summary>gpCamera->isLButtonCamera(): set by Mario each tick.</summary>
    public bool LCamera;
    /// <summary>unk5D60 & 0x40: landed drops within MergeReach x (size + size) of each other MERGE (move() 677-690: the earlier one takes the later one's size and
    /// life, the later one dies) -- the puddle is refreshed by every drop that lands in it and the pool does not fill with the sitting ones. Off = every landed
    /// drop its own puddle and its own clean stamp (254's balance).</summary>
    public bool SimulateMerge = true;
    /// <summary>A collider in this group is a wet (water-slip) plane: the slip friction, fall and vanish speed.</summary>
    public string SlipGroup = "sms_wet_ground";

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
    private readonly byte[] _state = new byte[MaxDrops];        // 1 flying, 2 landed (a floor), 3 landed (a wall) -- the game's low four flag bits
    private readonly Vector3[] _hit = new Vector3[MaxDrops];    // where a landed drop touches the map (the clean stamp's centre; 263: moves as it slides)
    private readonly Vector3[] _normal = new Vector3[MaxDrops]; // the surface normal there (the puddle's plane)
    private readonly bool[] _wall = new bool[MaxDrops];         // landed on a wall (splashWall's x 32) rather than a floor (splashGround's x 10)
    private readonly bool[] _slip = new bool[MaxDrops];         // 263: the plane under it is a wet one (SlipGroup)
    private int _count;
    private MultiMeshInstance3D _mmi;
    private MultiMesh _mm;
    private ShaderMaterial _mat;
    private MultiMeshInstance3D _mmiSil;                        // 263: the same instances drawn again in Mario's silhouette pass
    private ShaderMaterial _matSil;
    private readonly Random _rng = new Random(0x5A17);
    private int _dbgHits;
    private int _landedThisTick, _diedOnLanding, _merged, _slidOff;   // 263 readouts
    public const string SilShaderPath = "res://assets/Sms/Fx/water_silhouette.gdshader";
    public const string JumpTexPath = "res://assets/Sms/Fx/tex/waterjumping.png";   // /mario/timg/waterJumping.bti (I8 32 x 32): the silhouette's drop shape

    public int Count => _count;
    /// <summary>263: the landed drops (each its own puddle now that the merge is simulated).</summary>
    public int PuddleCount { get { int n = 0; for (int i = 0; i < _count; i++) if (_state[i] != 1) n++; return n; } }
    public int FlyingCount { get { int n = 0; for (int i = 0; i < _count; i++) if (_state[i] == 1) n++; return n; } }
    /// <summary>263, for the drives: since the last call -- drops that landed, that died in their landing splash, that merged into a puddle, that slid off an edge.</summary>
    public int[] TakeStats() { var s = new[] { _landedThisTick, _diedOnLanding, _merged, _slidOff }; _landedThisTick = _diedOnLanding = _merged = _slidOff = 0; return s; }
    /// <summary>263, for the drives: a landed drop's place, horizontal speed (metres a tick), life left, wall flag and slip flag, by index among the landed
    /// ([hit, speed, life, wall, slip]); an empty array past the end.</summary>
    public Godot.Collections.Array Landed(int k)
    {
        for (int i = 0; i < _count; i++)
            if (_state[i] != 1 && k-- == 0) return new Godot.Collections.Array { _hit[i], new Vector2(_vel[i].X, _vel[i].Z).Length(), _life[i], _wall[i], _slip[i] };
        return new Godot.Collections.Array();
    }
    public ShaderMaterial Material => _mat;
    public bool SilhouetteVisible => _mmiSil != null && _mmiSil.Visible;

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
        // 263: the silhouette twin -- the same MultiMesh drawn again where the depth test fails, in the silhouette colour (drawSilhouette); Mario's
        // SmsSilhouetteTick turns it on with his own silhouette and hands it the alpha
        var silShader = GD.Load<Shader>(SilShaderPath);
        if (silShader != null)
        {
            _matSil = new ShaderMaterial { ResourceName = "sms water silhouette", Shader = silShader };
            _matSil.SetShaderParameter("jump_tex", LoadTex(JumpTexPath));
            _matSil.SetShaderParameter("mask_tex", LoadTex(MaskTexPath));
            _matSil.SetShaderParameter("tail", Tail);
            _matSil.SetShaderParameter("still_len", SmsUnits.UnitsToMetres(1f));
            _mmiSil = new MultiMeshInstance3D
            {
                Name = "SmsWaterDropsSilhouette",
                Multimesh = _mm,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = _matSil,
                ExtraCullMargin = ExtraCullMargin,
                Visible = false,
            };
            AddChild(_mmiSil);
        }
    }

    /// <summary>263: Mario's silhouette pass (TSilhouette) takes the water with it -- drawSilhouette runs while the silhouette's alpha is above zero:
    /// <paramref name="alpha01"/> is that alpha's fraction of its 128 (0 = off), <paramref name="color"/> the stage's silhouette colour.</summary>
    public void SetSilhouette(float alpha01, Color color)
    {
        if (_mmiSil == null) return;
        bool on = alpha01 > 0.002f;
        _mmiSil.Visible = on;
        if (on) _matSil.SetShaderParameter("color", new Color(color.R, color.G, color.B, Mathf.Clamp(alpha01, 0f, 1f)));
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

    /// <summary>move(), one tick. A FLYING drop (case 1) gets its type's gravity, the one-size-a-tick horizontal cap and the step; a step that meets
    /// the map lands it -- a splash, and with the game's chance it SURVIVES as a puddle (else it dies there). 263: a LANDED drop (case 2 / 3) lives the
    /// game's way: its life capped at 256 and run down by the plane's vanish speed, its horizontal speed x the plane's friction + the plane's normal's
    /// horizontal part (so it drifts downhill and slides on a wet floor), its height put back on the floor each tick, a wall drop running down the wall
    /// (getPlaneFall) and dying where it meets the floor; a floor drop whose floor is gone from under it (slid off an edge) is a flying drop again
    /// (move() 660: the end of the hit chain sets the flag back to 1). Landed drops that overlap MERGE (unk5D60 & 0x40). The dead are compacted out.</summary>
    public void Tick()
    {
        if (_count == 0)
            return;
        var space = GetWorld3D().DirectSpaceState;
        float cull2 = CullDistance * CullDistance;
        float unit = SmsUnits.ArmatureScale;   // one game unit in metres
        for (int i = 0; i < _count; i++)
        {
            Vector3 p = _pos[i];
            float dx = p.X - MarioPos.X, dz = p.Z - MarioPos.Z;
            if (dx * dx + dz * dz > cull2)
            {
                _life[i] = 0f;   // unk5E08: too far from Mario in the horizontal
                continue;
            }
            Vector3 v = _vel[i];
            float size = _size[i];
            if (_state[i] == 1)
            {
                // pass 123b -- TObjHitCheck::checkWater (ObjHitCheck.cpp 97) + move() 424: every flying drop is a 50 x 80 cylinder tested against the
                // actors' damage cylinders; a hidden-coin spot / graffiti picture it overlaps takes the drop (HIT_MESSAGE_SPRAYED_BY_WATER) and the drop
                // dies there. The ray below only meets what the drop's path CROSSES; the game's test also counts a drop passing beside a spot.
                var zone = SmsHiddenCoin.WaterZoneAt(p);
                if (zone != null && zone.SmsSprayedByWater(p)) { _life[i] = 0f; continue; }
                var bud = SmsSamboFlower.ZoneAt(p);      // pass 129: a Pokey's sunflower bud (TSamboFlower's 30 x 120 hit actor) -- no body, the game's cylinder test
                if (bud != null && bud.SmsSprayedByWater(p)) { _life[i] = 0f; continue; }
                CapHorizontal(ref v, size, unit);
                v.Y += SmsParams.Fludd.WaterOf(_type[i]).Gravity * unit;
                Vector3 np = p + v;
                var rq = PhysicsRayQueryParameters3D.Create(p, np, CollisionMask);
                rq.CollideWithAreas = false;
                if (Exclude.IsValid)
                    rq.Exclude = new Godot.Collections.Array<Rid> { Exclude };
                var hit = space.IntersectRay(rq);
                if (hit.Count > 0)
                {
                    Vector3 n = (Vector3)hit["normal"];
                    Vector3 at = (Vector3)hit["position"];
                    // move() 429: a drop that meets an actor sends it HIT_MESSAGE_SPRAYED_BY_WATER; taken, the drop DIES there (mParticleLifetimeSOA
                    // = 0: no landing, no clean stamp -- the game adds a splash sound and, 30 % of the time, a TSplashManager splash, neither ported;
                    // the actor's own answer, the ENM_WATHIT mark, is its receiveMessage's). Pass 99 -- before this the drop sat and dried on the body.
                    var collider = hit["collider"].As<GodotObject>();
                    if (collider is ISmsSprayTarget target)
                    {
                        if (target.SmsSprayedByWater(at)) { _life[i] = 0f; continue; }
                    }
                    else if (DebugPrint && (++_dbgHits % 120) == 1 && collider is Node col && col is not StaticBody3D && col is not CsgShape3D)
                        GD.Print($"[SMS water] a drop hit {col.Name} ({col.GetType().Name}): not a spray target");
                    bool wall = n.Y < 0.5f; // the game asks the polygon's class (floor / wall); the port splits at 60 degrees
                    _landedThisTick++;
                    // move() 536-549 / 614-623: the splash; the size x mMagnify; then the chance to stay as a puddle (a floor: unk5D88[0], [1] under the L
                    // camera; a wall: [2]) -- else the drop is spent in the splash
                    float survive = wall ? WallSurviveChance : (LCamera ? LandSurviveChanceLCamera : LandSurviveChance);
                    if (_rng.NextDouble() >= survive) { _diedOnLanding++; _life[i] = 0f; continue; }
                    _wall[i] = wall;
                    _normal[i] = n;
                    _hit[i] = at;
                    _slip[i] = collider is Node cn && cn.IsInGroup(SlipGroup);
                    _pos[i] = at + n * (0.4f * unit);   // move() 522: pos.y = floor - gravity (0.4 u over it)
                    // the landing keeps the horizontal speed (case 2's slide starts from it; the game only zeroes the velocity on a roof hit), drops the vertical
                    _vel[i] = wall ? Vector3.Zero : new Vector3(v.X, 0f, v.Z);
                    _state[i] = (byte)(wall ? 3 : 2);
                    _life[i] = MathF.Min(_life[i], LandedLifeMax);   // unk5D68 / unk5D6C
                    CleanAt(i);
                    _life[i] -= _slip[i] ? VanishSlip : VanishNormal;
                }
                else
                {
                    _pos[i] = np;
                    _vel[i] = v;
                    _life[i] -= 1f;
                }
            }
            else if (_state[i] == 2)
            {
                // case 2: the slide -- friction x the speed + the plane normal's horizontal part (downhill, one unit a tick on a 45-degree slope);
                // then the floor under the new place takes it back onto itself (checkGroundIgnoreWaterThrough: the game's particle sits 0.4 u over the
                // floor and asks for a floor within a unit under it; here the ray spans half a drop size above and below the last contact's height, so
                // a slope's rise or fall under one tick's slide is always inside it, and only a real edge is not)
                float fr = _slip[i] ? FrictionSlip : FrictionNormal;
                Vector3 nrm = _normal[i];
                v.X = v.X * fr + nrm.X * unit;
                v.Z = v.Z * fr + nrm.Z * unit;
                v.Y = 0f;
                CapHorizontal(ref v, size, unit);
                Vector3 nxz = _hit[i] + new Vector3(v.X, 0f, v.Z);
                float reach = MathF.Max(size * 0.5f, 4f * unit);
                var rq = PhysicsRayQueryParameters3D.Create(nxz + new Vector3(0f, reach, 0f), nxz - new Vector3(0f, reach + unit, 0f), CollisionMask);
                rq.CollideWithAreas = false;
                if (Exclude.IsValid)
                    rq.Exclude = new Godot.Collections.Array<Rid> { Exclude };
                var hit = space.IntersectRay(rq);
                if (hit.Count > 0 && ((Vector3)hit["normal"]).Y >= 0.5f)
                {
                    var collider = hit["collider"].As<GodotObject>();
                    if (collider is ISmsSprayTarget target && target.SmsSprayedByWater((Vector3)hit["position"])) { _life[i] = 0f; continue; }   // move() 552: the floor's actor takes it
                    _normal[i] = (Vector3)hit["normal"];
                    _hit[i] = (Vector3)hit["position"];
                    _slip[i] = collider is Node cn && cn.IsInGroup(SlipGroup);
                    _pos[i] = _hit[i] + _normal[i] * (0.4f * unit);   // the game's: floor - gravity
                    _vel[i] = v;
                    CleanAt(i);   // splashGround runs every tick a landed drop lives
                    _life[i] -= _slip[i] ? VanishSlip : VanishNormal;
                }
                else
                {
                    // no floor under the new place: it slid off the edge -- a flying drop again, falling from here with its slide's speed
                    _slidOff++;
                    _pos[i] = nxz + new Vector3(0f, 0.4f * unit, 0f);
                    _vel[i] = new Vector3(v.X, SmsParams.Fludd.WaterOf(_type[i]).Gravity * unit, v.Z);
                    _state[i] = 1;
                    _life[i] -= 1f;
                }
            }
            else
            {
                // case 3: a wall drop runs down the wall (getPlaneFall) and dies where it meets the floor (move() 525: a case-3 drop whose ground check lands)
                float fall = (_slip[i] ? FallSlip : FallNormal) * unit;
                Vector3 nrm = _normal[i];
                Vector3 down = (Vector3.Down - nrm * Vector3.Down.Dot(nrm));   // down the wall's plane
                if (down.LengthSquared() > 1e-8f) down = down.Normalized();
                Vector3 np = p + down * -fall;   // fall is negative: this many units down
                var rq = PhysicsRayQueryParameters3D.Create(np + new Vector3(0f, unit, 0f), np - new Vector3(0f, unit, 0f), CollisionMask);
                rq.CollideWithAreas = false;
                if (Exclude.IsValid)
                    rq.Exclude = new Godot.Collections.Array<Rid> { Exclude };
                var floor = space.IntersectRay(rq);
                if (floor.Count > 0 && ((Vector3)floor["normal"]).Y >= 0.5f) { _life[i] = 0f; continue; }
                _pos[i] = np;
                _hit[i] = np - nrm * (0.4f * unit);
                CleanAt(i);   // splashWall runs every tick a wall drop lives
                _life[i] -= _slip[i] ? VanishSlip : VanishNormal;
            }
        }
        // move() 664-724: the landed drops MERGE -- in order, a landed drop within unk5D88[6] x (size + size) of the previous landed drop of its kind hands
        // that one its size and life and dies (the puddle under a stream is refreshed by every drop that lands in it); floors and walls separately
        if (SimulateMerge)
        {
            int lastFloor = -1, lastWall = -1;
            for (int i = 0; i < _count; i++)
            {
                if (_life[i] <= 0f || _state[i] == 1) continue;
                ref int last = ref (_state[i] == 2 ? ref lastFloor : ref lastWall);
                if (last < 0) { last = i; continue; }
                if (_hit[i].DistanceTo(_hit[last]) < MergeReach * (_size[i] + _size[last]))
                {
                    _size[last] = _size[i];
                    _life[last] = _life[i];
                    _life[i] = 0f;
                    _merged++;
                }
                else last = i;
            }
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
                _hit[w] = _hit[i]; _normal[w] = _normal[i]; _wall[w] = _wall[i]; _slip[w] = _slip[i];
            }
            w++;
        }
        _count = w;
    }

    /// <summary>move() 447: a drop never moves more than its own size (less a tenth of a unit) a tick in the horizontal (no tunnelling).</summary>
    private static void CapHorizontal(ref Vector3 v, float size, float unit)
    {
        float hor2 = v.X * v.X + v.Z * v.Z;
        if (hor2 > size * size)
        {
            float k = (size - 0.1f * unit) / MathF.Sqrt(hor2);
            v.X *= k;
            v.Z *= k;
        }
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

    /// <summary>The frame's instances: the puddles first (one a landed drop -- 263: the merge is simulated in Tick, so each landed drop is its own
    /// disc, shrinking with its life over LandedLifeMax), then every flying drop as the game's quad -- its transform the drop's place, its custom data
    /// the velocity x mExtension and the size the shader builds calcDrawVtx's corners from.</summary>
    public override void _Process(double delta)
    {
        if (_mm == null)
            return;
        int n = 0;
        // drawWaterVolume's discs: the volume's scale f = size x mMagnify (move() 539: the size is multiplied by mMagnify as the drop lands)
        // x the life left (calcVMAll: (1 - unk5D88[5] = 0) x life / unk5D68 + 0 -> straight to nothing); a floor disc of radius 11.76 f in the
        // surface's plane, a wall patch 8.5 f each side, 7.29 f up and 14.1 f down (water_shadow_kabe), each 2 units off the surface.
        float lift = SmsUnits.UnitsToMetres(PuddleLift);
        for (int i = 0; i < _count; i++)
        {
            if (_state[i] == 1)
                continue;
            float k = Mathf.Clamp(_life[i] / LandedLifeMax, 0f, 1f);
            float f = _size[i] * SmsParams.Fludd.WaterOf(_type[i]).Magnify * k;
            Vector3 nrm = _normal[i];
            Vector3 refv = MathF.Abs(nrm.Y) > 0.9f ? Vector3.Right : Vector3.Up;
            Vector3 ax = refv.Cross(nrm).Normalized();      // across the surface; on a wall: along it, horizontal
            Vector3 ay = nrm.Cross(ax);                     // on a wall: up the wall
            Vector3 centre = _hit[i] + nrm * lift;
            Basis b;
            if (_wall[i])
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
