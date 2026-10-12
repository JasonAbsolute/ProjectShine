using Godot;

/// <summary>One NPC TYPE as data: TNpcSaveIndividual (/params/npc/npc&lt;type&gt;.prm, every field, the game's units: units and
/// units/tick -- live actors add their full velocity every 120 Hz tick), the per-type behaviour switches TBaseNPC derives from
/// mActorType (NpcBase.cpp isMonte / isMadNpc / isBeTrampledNpc / isTurnToMarioWhenApproach), the clip per animation KIND
/// (NpcInitAnmData.cpp's index tables resolved to the archive's alphabetical .bck names, which are the glb's animation names), the
/// hit cylinders (NpcInitData.cpp TNpcInitInfo) and the colour tables (NpcInitData.cpp sMonteM_BodyColorBuf and friends --
/// GX S10 values, so a channel may exceed 255: the TEV register brightens the skin texture). Duplicate a .tres, change a number,
/// assign it to an NPC's Profile: that is the whole customization path.</summary>
[Tool, GlobalClass]   // [Tool] (pass 91): a [Tool] SmsNpc exports this type; a non-tool C# Resource is only a placeholder in the editor
public partial class SmsNpcProfile : Resource
{
    public enum NpcKind { PiantaMale, PiantaFemale, Toad, Other }

    [ExportGroup("Type")]
    [Export] public NpcKind Kind = NpcKind.PiantaMale;
    /// <summary>isBeTrampledNpc: Piantas, Nokis, Toads, Toadsworth squash and shake under a stomp.</summary>
    [Export] public bool Trampled = true;
    /// <summary>isMadNpc: only Piantas (M/W and their variants) get angry after a trample or a spray.</summary>
    [Export] public bool GetsMad = true;
    /// <summary>isBehaveToWaterNpc: reacts to FLUDD's water (the wet animation).</summary>
    [Export] public bool ReactsToWater = true;
    /// <summary>isTurnToMarioWhenApproach: ONLY Toads, Toadsworth and Peach (0x4000016..18) turn their whole body to Mario when he
    /// comes near; a Pianta keeps its placed facing (isNeedTurnToFirstState) and only turns for a talk.</summary>
    [Export] public bool TurnsToMarioOnApproach = false;
    /// <summary>isTurnToMarioWhenTalk: everyone but the mop / tube / sunflower / board NPCs.</summary>
    [Export] public bool TurnsToMarioWhenTalking = true;
    /// <summary>isPollutionNpc (NpcBase.cpp 265): the Piantas (M / MA / MC / W / WA), the Nokis (MARE_M / W) and the Toad sink into goop they
    /// stand on (TNerveNPCSink, SinkSpeed / SinkHeight), come out dirty (PollutionMax, the DIRTY wait, no talk, no turn) and are washed
    /// clean by the hose (PollutionCleanSpeed a hit). Off = the goop is just floor to this NPC.</summary>
    [Export] public bool SinksInPollution = true;

    [ExportGroup("Hit cylinders (units)")]
    [Export] public float AttackRadius = 100f, AttackHeight = 200f, DamageRadius = 100f, DamageHeight = 200f;

    [ExportGroup("npc*.prm")]
    [Export] public float BodyHeight = 230f;           // mSLBodyHeight
    [Export] public float CursorHeight = 30f;          // mSLCursorHeight: the talk cursor floats this far over the body
    [Export] public float LookatHeight = 40f;          // mSLLookatHeight: the camera's focal point is BodyHeight - this
    [Export] public float CircleShadowSize = 120f;
    [Export] public float MinWalkAnmRate = 0.1f, MaxWalkAnmRate = 1.5f, MaxRunAnmRate = 3f;
    [Export] public float MinMarchSpeed = 0.2f, MaxMarchSpeed = 1.2f, MaxRunSpeed = 5f;   // units / tick
    [Export] public float RunAccel = 0.08f, MarchAccel = 0.02f, MarchDecrease = 0.1f;    // units / tick^2
    [Export] public float WalkTurnSpeed = 1.5f, WaitTurnSpeed = 1f;                      // deg / tick
    [Export] public float TurnAnmMinRate = 0.7f, TurnAnmMaxRate = 2f, TurnAnmRate = 1.2f;
    [Export] public float WaitAnmOffDist0 = 3500f, WaitAnmOffDist1 = 7500f, LodChangeDist = 5000f, AllDLLockDist = 2000f;
    [Export] public float NeckTurnSearchDist = 1000f, NeckTurnSearchHeight = 600f;
    [Export] public float BodyTurnSearchDist = 250f, BodyTurnSearchHeight = 250f, BodyTurnSearchDegree = 90f, BodyTurnSearchAware = 300f;
    [Export] public float MadSearchDist = 800f, MadSearchHeight = 800f, MadSearchDegree = 240f, MadSearchAware = 500f;
    [Export] public float MadTurnSpeed = 20f, FirstStateTurnSpeed = 0.5f, UTurnSpeed = 3f;   // deg / tick
    [Export] public float SinkSpeed = 0.8f, SinkHeight = 220f;
    [Export] public int PollutionMax = 150;
    [Export] public float PollutionCleanSpeed = 0.008f;
    [Export] public int NeckMinAngleX = -3640, NeckMaxAngleX = 0, NeckMaxAngleY = 5460;     // BAM (32768 = a half turn)
    [Export] public int NeckAngleXSpeed = 500, NeckAngleYSpeed = 700;                      // BAM / frame

    [ExportGroup("Clips (NpcInitAnmData kind -> bck)")]
    [Export] public string ClipWalk = "mom_walk";        // kind 0
    [Export] public string ClipWait = "mom_wait";        // kind 1
    [Export] public string ClipFall = "mom_fall";        // kind 2
    [Export] public string ClipRecover = "mom_recover";  // kind 3
    [Export] public string ClipStep = "mom_step";        // kind 4
    [Export] public string ClipWet = "mom_wet";          // kind 5
    [Export] public string ClipTalk = "mom_talk";        // kind 6
    [Export] public string ClipRun = "mom_run";          // kind 8
    [Export] public string ClipTaken = "mom_wait";       // kind 9
    [Export] public string ClipMad = "mom_mad";          // kind 0xA
    [Export] public string ClipWetAlt = "mom_wet_a";     // kind 0xB
    [Export] public string ClipWaitA = "mom_wait_a";     // kind 0xC
    [Export] public string ClipThrow = "mom_throw";      // kind 0xD
    [Export] public string ClipDirty = "mom_wait_yogore"; // kind 0xF
    [Export] public string ClipWaitFall = "mom_wait_fall"; // kind 0x10
    [Export] public string ClipHappy = "mom_happy";      // kind 0x11
    [Export] public string ClipSit = "mom_sit";          // kind 0x12
    [Export] public string ClipSitTalk = "mom_sittalk";  // kind 0x13
    [Export] public string ClipSitWet = "mom_sitwet";    // kind 0x14
    [Export] public string ClipWaitB = "mom_wait_b";     // kind 0x15
    [Export] public string ClipDance = "mom_dance";      // kind 0x16
    [Export] public string ClipMadLoop = "mom_madloop";  // kind 0x17
    [Export] public string ClipWash = "mom_wet_yogore";  // kind 0x19
    [Export] public string ClipAppear = "mom_appear";    // kind 0x1A
    /// <summary>The neck joint the game's NPCNeckCallBack rotates toward Mario ("kubi" on every NPC).</summary>
    [Export] public string NeckBone = "kubi";

    [ExportGroup("Colours (GX S10, may exceed 255)")]
    /// <summary>The material whose register takes the body colour (_hand_mat on a Pianta, _mat_cap on a Toad).</summary>
    [Export] public string BodyMaterial = "_hand_mat";
    /// <summary>sMonteM_BodyColorBuf / sMonteW_ColorBuf / sKinopio_BodyColorData0, one entry per variant. ColorIndex picks one.</summary>
    [Export] public Vector3[] BodyColors = {
        new(100, 255, 300), new(120, 120, 300), new(350, 300, 0), new(200, 70, 0), new(300, 130, 255),
        new(255, 350, 0), new(400, 255, 255), new(320, 140, 0), new(200, 255, 400), new(400, 250, 100),
    };
    /// <summary>A second register on the same material (the Toad cap's white spots, sKinopio_BodyColorData1); empty = none.</summary>
    [Export] public Vector3[] BodyColors2 = System.Array.Empty<Vector3>();
    /// <summary>A second coloured material (the Toad's _mat_cloth) and its two registers (sKinopio_ClothColorData0 / 1); empty = none.</summary>
    [Export] public string ClothMaterial = "";
    [Export] public Vector3[] ClothColors = System.Array.Empty<Vector3>();
    [Export] public Vector3[] ClothColors2 = System.Array.Empty<Vector3>();

    public int ColorCount => BodyColors != null && BodyColors.Length > 0 ? BodyColors.Length : 1;

    [ExportGroup("Leaves (pass 95)")]
    /// <summary>The Pianta's leaf fringe material (_leafback_mat on mom_model / mow_model); "" = none (the Toad). NpcLeaf.gdshader = its bmd TEV.</summary>
    [Export] public string LeafMaterial = "_leafback_mat";
    /// <summary>The leaf texture the game swaps in for the model's 8 x 8 dummy (NpcManager.cpp: I_mom_mino_dummyI4 -> /scene/monteMCommon/
    /// I_mom_mino_rgba.bti -- EVERY stage ships its own; assets/Sms/Npc/tex carries Delfino Plaza's two). Empty = the Kind's default.</summary>
    [Export] public Texture2D LeafTexture;
    /// <summary>Materials whose pollution mask and goop are read through the SECOND UV set (the bmd's TEX1): the leaves; on the Toad all four
    /// (_mat_body / _mat_cap / _mat_cloth / _mat_mouth: texgen1 / 2 src TEX1 -- its glb DOES carry TEXCOORD_1, pass 100; passes 93 / 95 had
    /// read it as missing). The Pianta body reads TEX0 (_hand_mat texgen0 / 1 src TEX0).</summary>
    [Export] public string[] PollutionUv2Materials = { "_leafback_mat" };

    [ExportGroup("Textures (pass 100: the bmd's own -- the glb export's stand-ins are wrong)")]
    /// <summary>_eye_mat's texture: the bmd's texmap0 (I_mom_eye01_IA8 / I_mow_eye01_IA8 / N_kinopio_me_open: a dark oval with a white glint on
    /// a transparent sheet). Empty = the glb material's albedo, which FinModelUtility got WRONG: the Toad's is the toon ramp (white wedge eyes),
    /// the Piantas' carry the intensity with the top bit flipped (grey pupils). assets/Sms/Npc/tex, decoded from the bmd by bmd_mat_dump.py.</summary>
    [Export] public Texture2D EyeTexture;
    /// <summary>BodyMaterial's texture. The Toad's _mat_cap MASK N_kinopio_cap (I4 64 x 64: a black circle on white -- stage 1 mix(C1, C2, TEXC):
    /// black = the spot colour BodyColors, white = the cap BodyColors2). The glb gave it the pollution gradient (a pink wash, no spots). A Pianta's
    /// _hand_mat samples no texture: leave empty.</summary>
    [Export] public Texture2D BodyMaterialTexture;
    /// <summary>ClothMaterial's MASK: the Toad's N_kinopio_cloth (I4 32 x 32: a black border on white -- black = the trim ClothColors (orange),
    /// white = the vest ClothColors2). The glb gave it the pollution gradient too.</summary>
    [Export] public Texture2D ClothTexture;
    /// <summary>The Toad's _mat_body texture N_kinopio_body (the face / hands / shorts sheet, CMPR 64 x 64) -- the glb's was a lossy re-encode.</summary>
    [Export] public Texture2D SkinTexture;
    /// <summary>The Toad's _mat_mouth texture N_kinopio_kuti_close (the bmd's default and the mouth the patterns start from).</summary>
    [Export] public Texture2D MouthTexture;
    /// <summary>Pass 101: the kind's texture pattern animations (the disc's .btp files: blinks, mouths) -- TexPatterns/&lt;Kind&gt;.tres. The game
    /// pairs every anm kind with a bck and a btp (NpcInitAnmData.cpp sMonte* / sKinopio_AnmIndex): a Pianta clip with a same-named pattern
    /// plays it, every other clip the wink (two quick blinks every 1.5 s); the Toad's six cover its clips by table. Empty = no blinking.</summary>
    [Export] public SmsNpcTexPatternSet TexPatterns;
    [ExportGroup("Worried kind (pass 102)")]
    /// <summary>NPC_ACTION_UNK100 (the placement's "worried" flag; the Toad and the old Toad in the game) -> TLodAnm::setIndividualBck: a clip the
    /// worried kind swaps for another (NpcInitPrg.cpp sIndividualKinopioBck: wait -> waitororo, sit -> sitororo, run -> runororo, walk ->
    /// walkororo, talk -> worry, sittalk -> sitworry). Empty = this kind has no worried variant (the Piantas).</summary>
    [Export] public Godot.Collections.Dictionary<string, string> WorriedClips = new();
    /// <summary>setIndividualBtp: a texture pattern the worried kind swaps (sIndividualKinopioBtp: twait -> twait2, ttalk -> tworry).</summary>
    [Export] public Godot.Collections.Dictionary<string, string> WorriedPatterns = new();

    public string WorriedClip(string clip) => WorriedClips != null && clip != null && WorriedClips.TryGetValue(clip, out var w) && !string.IsNullOrEmpty(w) ? w : clip;
    public string WorriedPattern(string pattern) => WorriedPatterns != null && pattern != null && WorriedPatterns.TryGetValue(pattern, out var w) && !string.IsNullOrEmpty(w) ? w : pattern;

    [ExportGroup("Effects (NpcEffect.cpp)")]
    /// <summary>mHappyEffectMtxPtr / mPollutionEffectMtxPtr: the joint the sparkle, the centre drips and the wash are bound to -- koshi_null on a
    /// Pianta, jnt_body on a Toad (setHappyEffectMtxPtr_ / setPollutionEffectMtxPtr_).</summary>
    [Export] public string EffectJoint = "koshi_null";
    /// <summary>emitHappyEffect_: ms_mnt_kira (Piantas) / ms_mare_kira (Toads) when the HAPPY clip starts, x the NPC's scale x HappyEffectScale.
    /// Empty = the Kind's default scene (assets/Sms/Fx/NpcSparkle*.tscn).</summary>
    [Export] public PackedScene HappyEffect;
    /// <summary>mSLCleanEffectScale (npcnormal.prm): 1.65.</summary>
    [Export] public float HappyEffectScale = 1.65f;
    /// <summary>emitDirtyEffect_: the goop drips off the dirty idle -- ms_mnt_yogore_b (Piantas) / ms_kino_yogore (Toads) at EffectJoint when the
    /// DIRTY clip passes each of DirtyDripFrames (sCheckFrameMonte 28 52 76 128 152 176; Kino 22 44).</summary>
    [Export] public PackedScene DirtyDripEffect;
    [Export] public float[] DirtyDripFrames = { 28f, 52f, 76f, 128f, 152f, 176f };
    /// <summary>Piantas only: ms_mnt_yogore_f off the left foot at FootDripFramesL and the right at FootDripFramesR.</summary>
    [Export] public PackedScene FootDripEffect;
    [Export] public string FootJointL = "footL_jnt";
    [Export] public string FootJointR = "footR_jnt";
    [Export] public float[] FootDripFramesL = { 28f, 52f, 76f };
    [Export] public float[] FootDripFramesR = { 128f, 152f, 176f };
    /// <summary>emitWashEffect_: ms_mnt_wash / ms_mare_wash, one emitter at EffectJoint kept alive while the WASH clip plays.</summary>
    [Export] public PackedScene WashEffect;
    /// <summary>emitSinkEffect_ -> SMS_EmitSinkInPollutionEffect: the goop ripple ring (+ the balls while not at the bottom) at the sink point.</summary>
    [Export] public PackedScene SinkEffect;

    [ExportGroup("Burning (pass 98, NPC_ACTION_BURNING)")]
    /// <summary>NpcInitPrg.cpp 451: only a normal male Pianta (isNormalMonteM) burns -- the placement flag is ignored on any other kind.</summary>
    [Export] public bool CanBurn = true;
    /// <summary>setSmokeEffectMtxPtr_(false): the fire sits on this joint of the body (yashi_jnt, the palm on his head).</summary>
    [Export] public string FireJoint = "yashi_jnt";
    /// <summary>setSmokeEffectMtxPtr_(true): with the luggage part (parts slot 11, nimotsu_model) the fire sits on ITS ef_null joint -- the first attached
    /// part whose model carries this bone is taken as that part.</summary>
    [Export] public string FireJointOnLuggage = "ef_null";
    /// <summary>emitParticle_ (NpcEffect.cpp 233): ms_npc_kokuen + ms_moe_fire_a / _b / _c at the fire joint's position every frame while BURNING, the three
    /// fire emitters scaled by mBurnStrength. Empty = assets/Sms/Fx/NpcBurn.tscn.</summary>
    [Export] public PackedScene BurnEffect;
    /// <summary>Pass 99: a BURNING Pianta wears the Luggage. Every burning placement in the shipped stages (scene.bin, action index 24 = 0x4088)
    /// carries parts bit 0x800 -- the luggage slot: dolpic6..10's running Pianta (blue coin 4), dolpic_ex3's (HatG + Luggage), monte2's 'monte28'
    /// (coin 22), monte_ex0's -- 8 of 8 (npc_placements.py), so the game only ever shows the fire on the bag (setSmokeEffectMtxPtr_(true): ef_null).
    /// On: a Burning instance whose slots, extras and hand-placed parts carry no luggage gets <see cref="BurnLuggagePart"/> added (the editor preview
    /// shows it too). Off: the code's bare case -- the fire on the palm (yashi_jnt) -- which no shipped stage uses.</summary>
    [Export] public bool BurnWithLuggage = true;
    /// <summary>The luggage part BurnWithLuggage adds; empty = assets/Sms/Npc/Parts/Pianta/Luggage.tscn (the game's nimotsu_model, slot 11).</summary>
    [Export] public PackedScene BurnLuggagePart;
    /// <summary>Where a burning NPC's water-hit mark (PARTICLE_MS_ENM_WATHIT, one per hose hit) appears. Feet = the game: behaveToHitObject_ 351 emits
    /// it at the NPC's own mPosition (TMarioParticleManager::emit flag 0 = a one-shot at that point -- its feet, every splat stacked there). DEVIATIONS
    /// at Jaysonn's word (pass 99b, "it should be on the backpack area"): Fire = at the fire itself (the bag's ef_null, or the palm) -- the default;
    /// Drop = where the drop hit the NPC's hit sphere (the enemies' convention, TSmallEnemy 547).</summary>
    [Export] public WaterHitMarkPlace WaterHitMarkAt = WaterHitMarkPlace.Fire;
    public enum WaterHitMarkPlace { Feet, Drop, Fire }
    /// <summary>The Toads' drips have no foot emitters and getEffectScale_ is 1 for them (a Pianta's effects scale with its node).</summary>
    public bool IsToad => Kind == NpcKind.Toad;

    public static Color S10(Vector3 v) => new Color(v.X / 255f, v.Y / 255f, v.Z / 255f, 1f);
}
