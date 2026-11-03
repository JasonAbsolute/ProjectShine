using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// KING BOO -- TBossTelesa (Enemy/bosstelesa.cpp; JP ボステレサ, the btelesa archive) on <see cref="SmsSmallEnemy"/> with the shipped /enemy/bosstelesa.prm
/// (SMS pass 153, feature/enemy-sirena). Casino Delfino's boss: he hangs over the great roulette table with his SLOT MACHINE under him, laughing. Spray his
/// TONGUE and he spins the slot; three of a kind FRUIT and he spits a fan of fruit and two RED PEPPERS over the table; throw a pepper into his mouth (his
/// tongue) and he turns spicy and solid -- NOW a fruit on his body hurts him (a hit point, the damage clip, his colour the fruit's) and he hides, the
/// roulette discs spin faster. A fruit on him while he is not spicy only makes him flinch, lick and laugh (he EATS it). Three hits and he is down: the
/// Shine. Between rounds he hides for a while and comes back; while out he spits Boo attackers every few seconds. COIN result: ten coins. ENEMY / JACKPOT:
/// small enemies out of his mouth. Walk under him (within 300 u of the centre) and he vanishes.
///
/// THE GAME. TBossTelesa is a TSpineEnemy (ACTOR_TYPE_BOSS_TELESA; its own hit actor 30 x 420 attack / 30 x 260 damage at mPosition, the position
/// mGroundHeight + 300 over the table's centre, the model TransYOffset 800 + the hosei dip over that). bosstelesa.prm: HeadHeight 120, BodyRadius 30, 3 hit
/// points, GenAttackerTime 200, HitAngle 180, NumGenBubble 3, 1stBubbleSp 10, HideAreaRadius 1000, SlotItemNum 6, SlotFruitNum 16, SlotFirstHitCollectRate
/// 0.6, SlotHitCollectRate 1.0, TransYOffset 800, StopSlotTime 3500 / 3000 / 2500 (by his hit points 3 / 2 / 1), SpicyTime 1500; the statics mNormalAlpha
/// 0x96, mBaseHoseiPosY -300, mRouletteUpRate 0.03, mTelesaGenerateInterval 400. Three hit actors ride his joints: the BODY (TBossTelesaBody, 350 x 550 at
/// joint 1 jnt_body - 200 y: Mario -> ATTACK; a MAP_OBJECT -> checkHitObject; its SPRAYED_BY_WATER -> checkMessage: in PrepareSlot -> Freeze), the TONGUE
/// (180 x 350 at joint 7 jnt_sita_2 - 350 y: Mario -> ATTACK; a RED_PEPPER -> setSpicy; an object over tongue.y + 100 -> checkHitObject; SPRAYED -> tongueHitWater:
/// in Appear -> SlotStart) and the KILL cylinder (400 x 300 at (joint 1 xz, y - 350): every HIT_CATEGORY_ENEMY in it is killed; Mario within 300 u of its
/// point -> forceHide and unk6C = 1, which holds HideWait). The bck table is the archive's order (0 appear 59, 1 bero_hit 59, 2 damage 219, 3 down 199, 4
/// hide 44, 5 hit 42, 6 laugh 89, 7 lick 140, 8 / 9 / 10 the hot-spring bubbles 122 / 5 / 80 L, 11 roll 139, 12 spicy 30 L, 13 spicy_end 59, 14 spit 99, 15
/// wait 60 L, 16 wet 59). NERVES: FallDemo (the intro: Mario rides the top disc down, the slot drops into place) -> HideWait (HIDDEN, spicy off, the alpha
/// 0x96 + 30 x lost hit points, the slot scale 0; past 400 ticks with Mario not under him -> Appear, HIDDEN off) -> Appear (the appear clip, the BGM, the slot
/// scale 1 + randomReset, all collision ON; clip end -> wait; frame 78 -> the SLT_LAND shake; past 800 ticks every (400 + 100 x lost HP) ticks the spit
/// clip -> genAttacker at its frame 40: a Boo attacker; hoseiPosY x 0.96) -- the tongue sprayed -> SlotStart (the roll clip; frame 53 slotStart: the drums
/// spin, telesaForceKill; clip end -> wait, forceStopSlot(1); checkSlot: no drum held -> SpitSlotItem; hosei x 0.99) -> SpitSlotItem (past 200 ticks hosei -100
/// a tick; under -500 the spit clip -> generateSlotItem at frame 40; clip ended and past 600 -> PrepareSlot, fruitCollisionOn) -> PrepareSlot (wait; spicy:
/// bero_hit -> spicy (loop) until SpicyTime -> spicy_end -> plain again; unk368 counts to StopSlotTime (x 0.5 for a COIN round); the last 120 ticks the items
/// blink; every item dead or the time up -> forceAllItemKill, DISAPPEAR / ESCAPE, Hide) -> Hide (the hide clip; its end: collision OFF, rouletteStart (each
/// disc 0.05..0.1 + 0.03 x lost HP deg a tick, the middle one the other way; the shake) -> HideWait). checkHitObject (PrepareSlot only): coconut -> the body
/// tint (0xE6, 0x64, 0xB4) + kill; papaya / pineapple -> (0xE6, 0xB4, 0) + kill; durian -> (0x96, 0x32, 0xE6) + kill; a pepper is just eaten; the fruit dies
/// (makeObjDead). kill -> Die (pushed): spicy -> decHitPoints; collision OFF; hit points left: spicy -> brk 1 + the damage clip (the shake), its end ->
/// damageRecover (the fruit / peppers / coins killed with puffs, Hide pushed) ; not spicy -> the hit clip + brk 0 -> lick (brk 2) -> laugh -> collision ON,
/// wait, PrepareSlot again. Hit points 0: the down clip + brk 1, the slot scale 0, the DOWN sound; its end: the down puffs (coloured by the last fruit),
/// forceAllItemKill, slotStop; 240 ticks later the Shine (makeShineAppearWithDemo), DEAD. setSpicy (not in SpitSlotItem, not during bero_hit): spicy, the
/// bero_hit clip, the pepper killed. Freeze (the body sprayed in PrepareSlot): the wet clip + "thank you"; its end: spicy off, the alpha back. moveObject:
/// mPosition.y = mGroundHeight + 300; in Die the alpha +1 a tick toward 0xFF (solid) or, through the lick past frame 50 and the laugh, -1 toward 0x96 + 30 x
/// lost HP. generateSlotItem: FRUIT -> 16 fruit (the pool: 6 coconut, 6 papaya, 2 pineapple, 6 durian; slots 0 and 4 are the two RED PEPPERS, the first at
/// double speed) from the mouth (joint 5) - 50 y, 200 u out along a scattered fan of 160 deg, speed 6..10 u a tick, y -2, scale 1.5, NO_COLLISION until
/// PrepareSlot; COIN -> 10 coins (a 120-deg fan, (0.8..3.5) x 10 u a tick, y 10, from the mouth - 250 y, life 960; three coin rounds at most (unk370));
/// ENEMY -> 6 small enemies of kind 1 (Hamukuri) at 3 HP or 2 (Yakikuri) below, JACKPOT -> 6 of cycling kinds 1..7 (8 at 1 HP) from {Bubble, Hamukuri,
/// Yakikuri, BombHei, PoiHana, ElecNokonoko, Popo, Gesso, TobiPuku}, Mixed -> 12 Bubbles; each from the mouth - 250 y at (0.5..1) x 10 u a tick, y 2 x (2 +
/// 0.5..1), initAttacker. The slot (TTelesaSlot) is <see cref="SmsTelesaSlot"/>; the discs (TRoulette) <see cref="SmsRoulette"/>.
///
/// THE PORT. The boss never moves: his position is the floor under him + 300 u each tick (the ray takes MAP bodies only: the discs); _noBind, FlyBind,
/// the collision shape disabled (the game's own 30 x 420 cylinder is a hit check, not a wall). The body and the tongue are two <see cref="SmsBossTelesaPart"/>
/// StaticBody3D cylinders on the hidden-coin layer (the hose's drops hit them; nothing else does) following the joints, plus the cylinder maths against
/// Mario and the fruit each hit tick (every fourth, the game's lists). The four BossTelesaToon materials are the bmd's own TEV (pass 153b, j3d_dump153.py):
/// per-body copies carry K0_A as `opacity` (the screen MIX, 150 / 255 normal), K1 as `tint` (the fruit colour) and K2_A as `mask_k` (the .brk tables:
/// btelesa_damage.brk sweeps 0 -> 150 in 20 frames on the flinch, btelesa_lick.brk holds 150 then washes to 0 by frame 99; btelesa_down.brk's K0_A 0 ->
/// 255 in 30 frames on the spicy hit and the down); the .btp face patterns swap the eye sheet (btelesa_twait: the 150-frame blink loop; btelesa_tspicy:
/// the rolled eyes). The slot machine is the child Slot (TelesaSlot.tscn) placed from his joints as the game does (the hosei dip carries it out of sight
/// after the spit and it rises back on the appear); the discs are found by the exports or the `sms_roulette` group (top / middle / bottom). The fruit are
/// the port's Fruit scenes, the coins yellow_coin, the enemies the species scenes in <see cref="SlotEnemyScenes"/>, the Boo attackers Telesa.tscn bodies
/// pooled under the parent (SmsInitAttacker). The JPA effects (ms_btls_*): chika_a / b (the twinkle around him) and glow (at the body) every tick he shows,
/// yodare1-3 (the drool at the mouth and the tongue tip) but through the hide / laugh / spicy clips, spicy_hit (the first 20 frames of bero_hit at the mouth),
/// ase + spicy_a..d (the sweat at the body, the fire at the tongue tip through the spicy loop), fhit (+ _pe / _or / _gr by the fruit), damage, down (+ its
/// colours), fubuki (the fanfare's confetti), tls_change (the swallowed items) -- the port's Fx scenes and this scene's own emitters. The hint balloons
/// (0xE000F once at the first plain fruit hit, 0xE0010 past two more, 0xE0011 once at the first hit of all) through the HUD's SmsHintBalloon with the texts as
/// knobs; the rumble (rouletteStart, the attacker spit) on the pad; the camera shake tables from params/camera/shakebtelesa*.prm; the boss BGM a player
/// knob (started at the first appear, stopped at the down). Knobs (grouped, plain English): The arena (and how high he hangs), The fight (hit points, the
/// wait, the result pause, the round times, the slot rates and a forced result for the custom fight), What he spits, Deviations (speed scales, the Boo
/// attackers, the hide-when-under, the intro ride), Looks, Effects, Hints, Camera, Sounds. DEVIATIONS (logged): the intro ride (the game's FallDemo) is a
/// knob, off by default (his floor is the table: Jaysonn places Mario); the TBubble (hot-spring bubble) family is not ported -- it is dead code in the game
/// too (unk150 is always 1: the attacker is a Boo) and a Mixed result cannot happen with the shipped rates; the roll demo camera (btelesa_roll_camera.bck)
/// and the camera's Y offset chase are not ported (the port has no demo camera); the indirect wobble of the screen is off in the bmd; Gesso maps to the
/// small MameGesso, ElecNokonoko / Popo to nothing (skipped); a banana counts as a papaya; the spit waits for the LAST DRUM TO STOP before its 200-tick
/// pause (the game counts from the last release, which can drop the machine while that drum still creeps); he hangs HoverHeight over the table (the game's
/// 300 + TransYOffset 800 = 1100 u, lowered to 850 by default on Jaysonn's call); the appear clip's frame-78 landing shake never fires (the clip has 59
/// frames -- in the game too).
/// </summary>
public partial class SmsBossTelesa : SmsSmallEnemy
{
    // ------------------------------------------------------------------------------------------------------------------ bosstelesa.prm
    public class BossTelesaParams : Params
    {
        public int GenAttackerTime = 200, GenBubbleTime = 300, NumGenBubble = 3, SlotItemNum = 6, SlotFruitNum = 16, StopSlotTime0 = 3500, StopSlotTime1 = 3000, StopSlotTime2 = 2500, SpicyTime = 1500;
        public float HitAngle = 20f, FirstBubbleSp = 10f, HideAreaRadius = 500f, SlotFirstHitCollectRate = 0.6f, SlotHitCollectRate = 1f, TransYOffset = 800f;
    }
    /// <summary>The shipped /enemy/bosstelesa.prm (params.szs on his disc, sms_shipped_params.json).</summary>
    public static readonly BossTelesaParams Prm = new BossTelesaParams
    {
        HeadHeight = 120f, BodyRadius = 30f, WallRadius = 50f, ClipRadius = 300f, HitPointMax = 3, InstanceNum = 1, ActiveEnemyNum = 1,
        DamageRadius = 30f, DamageHeight = 260f, AttackRadius = 30f, AttackHeight = 420f, BodyScaleLow = 1f, BodyScaleHigh = 1f, TurnSpeedLow = 1f, TurnSpeedHigh = 1f, MarchSpeedLow = 1f, MarchSpeedHigh = 1f,
        GenAttackerTime = 200, GenBubbleTime = 300, HitAngle = 180f, NumGenBubble = 3, FirstBubbleSp = 10f, HideAreaRadius = 1000f, SlotItemNum = 6, SlotFruitNum = 16,
        SlotFirstHitCollectRate = 0.6f, SlotHitCollectRate = 1f, TransYOffset = 800f, StopSlotTime0 = 3500, StopSlotTime1 = 3000, StopSlotTime2 = 2500, SpicyTime = 1500,
    };
    protected override Params P => Prm;
    /// <summary>TBossTelesa's statics: mNormalAlpha, mBaseHoseiPosY, mRouletteUpRate, mTelesaGenerateInterval; the position over the floor (moveObject).</summary>
    public const int NormalAlpha = 0x96, TelesaGenerateInterval = 400, HideWaitTicksGame = 400, AppearSpitAfterTicks = 800, DownToShineTicks = 240;
    public const float BaseHoseiPosY = -300f, RouletteUpRate = 0.03f, PositionOverFloor = 300f;
    /// <summary>The three hit actors (init): the body 350 x 550 at joint 1 - 200; the tongue 180 x 350 at joint 7 - 350; the kill cylinder 400 x 300 at y - 350; Mario
    /// within 300 u of the kill point hides him (TBossTelesaKillSmallEnemy::checkHit).</summary>
    public const float BodyHitRadius = 350f, BodyHitHeight = 550f, BodyHitDrop = 200f, TongueHitRadius = 180f, TongueHitHeight = 350f, TongueHitDrop = 350f,
        KillRadius = 400f, KillHeight = 300f, KillDrop = 350f, HideMarioRadius = 300f, TongueObjectOver = 100f;

    // ------------------------------------------------------------------------------------------------------------------ the knobs
    public enum SlotForce { TheGame, Coin, Enemy, Fruit, Jackpot }
    [ExportGroup("The arena (empty = found: the sms_roulette group by size, the child Slot)")]
    /// <summary>The three roulette discs (the game's rulet00 / 01 / 02: the centre disc, the middle ring, the outer ring). Empty: the SmsRoulette bodies in the
    /// `sms_roulette` group, smallest first.</summary>
    [Export] public SmsRoulette RouletteTop, RouletteMiddle, RouletteBottom;
    /// <summary>His slot machine (TelesaSlot.tscn). Empty: the child named Slot.</summary>
    [Export] public SmsTelesaSlot Slot;
    /// <summary>The Shine 240 ticks after his down clip (makeShineAppearWithDemo): a ShineSprite scene. Empty = res://shine_sprite.tscn.</summary>
    [Export] public PackedScene ShineScene;
    /// <summary>Where it appears. Empty: at his point (300 u over the table's centre, where the slot hung).</summary>
    [Export] public Node3D ShineSpawnPoint;
    /// <summary>How high his body's centre hangs over his floor -- the table's centre: the pressed pond's bottom (143 u under the pads), metres. The game:
    /// mGroundHeight + 300 + TransYOffset 800 = 1100 u = 14.8 m over the pond; 12.33 m (918 u) after Jaysonn's first playtest ("he sits too high"), 13 m
    /// (968 u) after his third ("sit above the slot machine a bit more"): his body's bottom (284 u under his centre) 9.2 m up.</summary>
    [Export(PropertyHint.Range, "4,25,0.01")] public float HoverHeight = 13f;
    /// <summary>How far under his body's centre the slot machine's origin hangs while he sits on it, units. The game: 700 (joint 0 - 700) -- but the cabinet is
    /// 642 u tall from its origin, so the game's machine reaches 58 u under his centre and his body (284 u down) sinks 226 u into it. 960 here (Jaysonn:
    /// "slot machine should be a little lower"): the cabinet's top 318 u under his centre -- 34 u of air under his body. With HoverHeight 13 m the
    /// cabinet stands in the pressed bowl (its base 22 u under the bowl's bottom, its drums 100..500 u up from the bowl).</summary>
    [Export(PropertyHint.Range, "400,1600,10")] public float SlotUnderUnits = 960f;
    [ExportGroup("The fight (the game's numbers; change them for a custom fight)")]
    /// <summary>His hit points (bosstelesa.prm 3): one spicy fruit hit each.</summary>
    [Export(PropertyHint.Range, "1,20,1")] public int HitPoints = 3;
    /// <summary>How long he stays hidden between rounds, ticks (TNerveBossTelesaHideWait: 400, and until Mario leaves the centre). Also his first appearance.</summary>
    [Export(PropertyHint.Range, "0,6000,10")] public int HideWaitTicks = HideWaitTicksGame;
    /// <summary>How long the stopped slot shows its result (the fanfare) before the machine sinks and he spits, ticks (TNerveBossTelesaSpitSlotItem's 200 --
    /// the game counts them from the last drum's RELEASE, so its machine could drop while that drum still crept; here from the last STOP).</summary>
    [Export(PropertyHint.Range, "0,1200,10")] public int ResultPauseTicks = 200;
    /// <summary>x the round times (StopSlotTime 3500 / 3000 / 2500 ticks by his hit points; a coin round half): how long his fruit stay on the table before he
    /// swallows the round and hides. 1 = the game.</summary>
    [Export(PropertyHint.Range, "0.25,6,0.05")] public float RoundTimeScale = 1f;
    /// <summary>The slot's rates (SlotFirstHitCollectRate 0.6, SlotHitCollectRate 1.0): the chance the first stop is steered to FRUIT (COIN with Mario at 3 HP or
    /// under; 0.9 at 1 HP), and the chance each later drum is steered to match -- 1 = the shipped slot always lands three of a kind.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float FirstHitCollectRate = 0.6f, HitCollectRate = 1f;
    /// <summary>Force every round's result (a custom fight or a test). TheGame = the slot decides.</summary>
    [Export] public SlotForce ForceResult = SlotForce.TheGame;
    /// <summary>How many COIN rounds he pays out at most (unk370: 3); after that a coin result becomes ENEMY.</summary>
    [Export(PropertyHint.Range, "0,20,1")] public int CoinRounds = 3;
    [ExportGroup("What he spits (empty = the port's scene / skipped)")]
    /// <summary>The fruit pool's scenes (res://fruit_coconut.tscn, fruitpapaya, fruit_pineapple, fruit_durian, fruit_red_pepper) and the coin (res://yellow_coin.tscn).</summary>
    [Export] public PackedScene CoconutScene, PapayaScene, PineappleScene, DurianScene, RedPepperScene, CoinScene;
    /// <summary>The small enemies by the game's kind (0 Bubble, 1 Hamukuri, 2 Yakikuri, 3 BombHei, 4 PoiHana, 5 ElecNokonoko, 6 Popo, 7 Gesso, 8 TobiPuku). Empty =
    /// the port's: -, Hamukuri, FireHamukuri, Bomb/BombHei, Poihana, -, -, MameGesso, Puku. A missing kind spits nothing.</summary>
    [Export] public Godot.Collections.Array<PackedScene> SlotEnemyScenes = new();
    /// <summary>The Boo attacker (genAttacker: a TTelesa of the stage's manager made an attacker). Empty = res://assets/Sms/Enemy/Telesa.tscn.</summary>
    [Export] public PackedScene BooScene;
    /// <summary>How many Boo attackers can be out at once (the stage's TTelesaManager pool).</summary>
    [Export(PropertyHint.Range, "0,20,1")] public int MaxAttackers = 6;
    [ExportGroup("Deviations (1 / on = the game)")]
    /// <summary>x the discs' spin (rouletteStart's 0.05..0.1 + 0.03 a lost hit point, degrees a tick).</summary>
    [Export(PropertyHint.Range, "0,4,0.05")] public float RouletteSpeedScale = 1f;
    /// <summary>x the speed of what he spits (fruit 6..10 u a tick, coins, enemies, the attackers' first lift).</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float SpitSpeedScale = 1f;
    /// <summary>Spits a Boo attacker every (400 + 100 x lost HP) ticks while he waits for the spray (TNerveBossTelesaAppear). Off = he only laughs.</summary>
    [Export] public bool SpitsBooAttackers = true;
    /// <summary>153h (Jaysonn: "the boos should be able to be jumped on"): a jump or a ground pound pops a spat Boo attacker without the hose first
    /// (SmsTelesa.JumpPopsUnsprayed set on each one he spits). Off = the game: the hose stuns it, then the jump.</summary>
    [Export] public bool AttackersJumpable = true;
    /// <summary>153j: where a spat Boo appears -- this far in front of his mouth joint along his facing (the joint is 220 u inside the head; 250 = the lips).
    /// 0 = the game's: at the joint, inside the skull (Jaysonn: "king boo is spawning them behind himself").</summary>
    [Export(PropertyHint.Range, "0,600,10")] public float AttackersSpawnForwardUnits = 250f;
    /// <summary>153j: a spat Boo's first swoop heads for MARIO (SmsTelesa.FirstPointTowardMario). Off = the game's: 1000 u toward the WORLD's yaw 180 (the
    /// casino's layout; in a placed arena that is wherever -- behind him, as it happened).</summary>
    [Export] public bool AttackersFirstPointTowardMario = true;
    /// <summary>153j: the spat Boos home on Mario in a curve and swoop past him (SmsTelesa.SmoothAttack). Off = the game's: a fixed point 0.7..1.6 of the way to
    /// where Mario was, then the next -- straight legs and sharp turns (Jaysonn: "they should more gracefully follow Mario").</summary>
    [Export] public bool AttackersSmooth = true;
    /// <summary>The table's walking surface, units: the highest of the three discs' faces (the game's absolute 500 for the casino table) -- the spat Boos' floor.
    /// Without the discs: his floor + 143 (the pads over the pressed bowl's bottom).</summary>
    public float TableSurfaceUnits
    {
        get
        {
            float best = float.MinValue;
            for (int i = 0; i < 3; i++) { var r = RouletteAt(i); if (r != null && IsInstanceValid(r)) best = MathF.Max(best, r.FaceYUnits); }
            return best > float.MinValue ? best : _floorY + 143f;
        }
    }
    public float YawDeg => _yaw;
    /// <summary>The kind of the last fruit that hit him (checkHitObject) -- a drive reads which fruit's tint he wears.</summary>
    public Fruit.FruitType LastFruitType { get; private set; }
    /// <summary>The game's placement of the machine without the hosei and without his dip (his clips' motion only), units -- the seat the machine rides.</summary>
    public float SlotSeatUnits => _slotSeatPrev;
    /// <summary>Hides (and cancels the round) when Mario comes within 300 u of the table's centre under him (the kill cylinder's Mario test).</summary>
    [Export] public bool HidesWhenMarioUnder = true;
    /// <summary>The intro's hide clip on the first appearance too (the game: he is hidden by the FallDemo; the first HideWait runs its full wait).</summary>
    [Export] public bool StartHidden = true;
    /// <summary>The game's FallDemo (TNerveBossTelesaFallDemo): the centre disc starts IntroLiftUnits over the table (scene.bin places rulet00 2000 u over the
    /// other two) and sinks IntroFallSpeed u a tick with Mario on it -- the boss TAKES him (HIT_MESSAGE_TAKE: he is carried, no controls) and lets go
    /// 3 u before the bottom (RLT_SET) -- while the intro camera plays (btelesa_roll_camera.bck, 16.6 s: from 12 m over the table at the +z side,
    /// looking up at the pedestal, rising and circling to the -z wall looking down); level, the demo is over and Mario is IN CONTROL (rouletteFall's
    /// onNeutralMarioKey stops with the fall; the room's fixed camera takes the view) while the slot machine falls to its place UNSEEN (its scale is 0
    /// until the appear), the discs start turning as it passes (rouletteStart) and the fight begins with HideWait. Off: the fight starts with him hidden
    /// and the table flat.</summary>
    [Export] public bool IntroRide = false;
    /// <summary>When the ride starts (153l). WhenMarioEntersRoom: the disc waits raised until Mario is within IntroTriggerRadiusUnits of the table's centre
    /// (over the table), then he is TAKEN onto it as the game's boss takes him; WhenMarioLandsOnDisc: when he stands on the raised disc himself;
    /// AtOnce: the moment the scene starts (place him on the raised disc yourself -- 153b's).</summary>
    [Export] public IntroStarts IntroStart = IntroStarts.WhenMarioEntersRoom;
    public enum IntroStarts { WhenMarioEntersRoom, WhenMarioLandsOnDisc, AtOnce }
    /// <summary>WhenMarioEntersRoom's reach from the table's centre (2450 u: the room's wall).</summary>
    [Export(PropertyHint.Range, "500,6000,10")] public float IntroTriggerRadiusUnits = 2450f;
    /// <summary>How high the centre disc starts for the intro ride, units (2000: scene.bin's rulet00 at y 2000 over the others' 0, all three then lifted 500 by
    /// TRoulette::initMapObj).</summary>
    [Export(PropertyHint.Range, "100,3000,10")] public float IntroLiftUnits = 2000f;
    /// <summary>The disc's fall, units a tick (rouletteFall: 1 -- 2000 u in 16.7 s, the intro camera's length).</summary>
    [Export(PropertyHint.Range, "0.25,8,0.25")] public float IntroFallSpeed = 1f;
    /// <summary>The intro camera (fireStartDemoCamera("btelesa_roll_camera") at TBossTelesa::init): IntroCameraPath played on the game camera through the fall,
    /// in the table's frame. Off: the room's own camera watches the ride.</summary>
    [Export] public bool IntroCamera = true;
    /// <summary>The camera path (the bck baked: BtelesaRollCamera.tres).</summary>
    [Export] public SmsCameraPath IntroCameraPath;
    /// <summary>153b's look: the slot machine SEEN falling out of the sky after the disc lands (the game keeps it at scale 0 until the appear).</summary>
    [Export] public bool IntroSlotShown = false;
    [ExportGroup("Looks (the bmd's materials)")]
    /// <summary>The transparency slider: how solid he is. 1 = solid (Jaysonn's call, the default); the game's ghost is 0.588 (mNormalAlpha 0x96 -- K0 alpha, the
    /// body MIXED with the picture behind him by this, + 30 / 255 for every hit point he has lost, solid while he is hit).</summary>
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float GhostOpacity = 1f;
    /// <summary>The drool at all (Jaysonn's toggle, 153e): off = no strands, live -- turned off while he plays, the strands stop that tick.</summary>
    [Export] public bool DroolOn = true;
    /// <summary>The drool (ms_btls_yodare1 / 2 at the mouth, yodare3 at the tongue tip): the port's strand scenes (the Goopy Piranha's GkYodareL / GkYodareS,
    /// SmsGoopStrand ribbons) recoloured to spit. Empty = those; DroolFxScene empty AND DroolSmallFxScene empty = no drool.</summary>
    [Export] public PackedScene DroolFxScene, DroolSmallFxScene;
    /// <summary>How far the strands hang, as a multiple of the Piranha's (its 1.4 m for the long ones); their width likewise; the rounded tip (0 = cut flat).</summary>
    [Export(PropertyHint.Range, "0.25,6,0.05")] public float DroolLength = 2.2f, DroolWidth = 1.6f;
    [Export(PropertyHint.Range, "0,0.8,0.01")] public float DroolRoundTip = 0.35f;
    /// <summary>How much of his motion the hanging strands ride along with (SmsGoopStrand.FollowAnchor): 0 = the Piranha's (the points stay where they were
    /// born, so his rise with the slot leaves a 7 m streak behind the mouth for a dozen frames); 1 = rigidly under the mouth; 0.85 = a little sway.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float DroolFollow = 0.85f;
    /// <summary>Where the two mouth strands hang from: the mouth JOINT sits 220 u inside his head (jnt_mouth at z 98, the face at z 318), so the game's yodare1 / 2
    /// point is buried in a solid Boo -- the strands are moved DroolLipUnits forward along the joint's X (his forward) to the lower lip and DroolSpreadUnits
    /// either side along its Z (his sideways) to the lip's corners, beside the tongue (the fangs sit at +-217 u, the tongue is ~200 u wide). 0 / 0 = the joint.</summary>
    [Export(PropertyHint.Range, "0,400,5")] public float DroolLipUnits = 210f, DroolSpreadUnits = 200f;
    /// <summary>The strands' ribbons turned to face the camera (one strip each; SmsGoopStrand.FaceCamera): round-looking from the table, looking up at him.
    /// Off = the Piranha's two crossed strips, an X seen from below.</summary>
    [Export] public bool DroolFaceCamera = true;
    /// <summary>The strands' colours: the pale spit and its shade (the Piranha's are its goop's pair).</summary>
    [Export] public Color DroolLight = new Color(0.82f, 0.9f, 1f), DroolDark = new Color(0.45f, 0.58f, 0.8f);
    /// <summary>153c: the slot machine's own motion, units a tick -- how fast it SINKS below the table after the spit and at his hide (the game drops it 100 u a
    /// tick with the hosei: gone within a frame) and how fast it RISES back to him; it never sinks deeper than SlotHideDepthUnits under his floor.</summary>
    [Export(PropertyHint.Range, "0.5,100,0.5")] public float SlotSinkSpeed = 4f, SlotRiseSpeed = 40f;
    [Export(PropertyHint.Range, "200,4000,10")] public float SlotHideDepthUnits = 1500f;
    /// <summary>The eye sheets the .btp patterns swap in: open / half / close (btelesa_twait's blink: 19 open, 2 half, 3 closed, 2 half, 2 open, 2 half, 3 closed, 2
    /// half, 50 open, 2 half, 3 closed, 2 half, 58 open; 150 frames, looped) and roll (btelesa_tspicy: the rolled eyes while spicy). Empty = the models' pngs.</summary>
    [Export] public Texture2D EyeOpen, EyeHalf, EyeClose, EyeRoll;
    [ExportGroup("Effects (empty = the default / none)")]
    /// <summary>ms_btls_fhit: the splat where a fruit hits him. Empty = assets/Sms/Fx/HitBurst.tscn.</summary>
    [Export] public PackedScene FruitHitFxScene;
    /// <summary>ms_btls_fhit_pe / _or / _gr: the puff in the fruit's colour beside the splat (the scene's prm / env recoloured). Empty = assets/Sms/Fx/EnemyDisap.tscn.</summary>
    [Export] public PackedScene FruitPuffFxScene;
    /// <summary>ms_btls_damage: the spicy hit's burst. Empty = assets/Sms/Fx/EnemyDisap.tscn.</summary>
    [Export] public PackedScene DamageFxScene;
    /// <summary>ms_btls_down (+ _pe / _or / _gr in the last fruit's colour): the down clip's puffs at his body. Empty = assets/Sms/Fx/EnemyDisap.tscn.</summary>
    [Export] public PackedScene DownFxScene;
    /// <summary>ms_tls_change (0xCD): the puff where a spat fruit / coin / enemy is swallowed back. Empty = assets/Sms/Fx/EnemyDisap.tscn.</summary>
    [Export] public PackedScene ItemKillFxScene;
    /// <summary>ms_btls_fubuki: the confetti of a FRUIT / COIN fanfare over the slot. Empty = assets/Sms/Enemy/BossTelesa/BtlsFubuki.tscn.</summary>
    [Export] public PackedScene FanfareFxScene;
    /// <summary>ms_btls_spicy_a..d: the fire off his tongue tip (joint 9 jnt_sita_4) through the spicy loop -- an SmsNpcEffect scene, started and stopped with the
    /// clip. Empty = assets/Sms/Fx/BtlsSpicyFire.tscn (153g: the four .jpa of the disc's btelesa archive, gen_fx153g.py: 350-u flame stripes shooting 24 u a
    /// tick off the tip, a 650-u glow, the heat haze, falling sparks); 153b..f used the Stu's fire x 2.5.</summary>
    [Export] public PackedScene SpicyFireFxScene;
    /// <summary>The fire's scale (the game's 1: the archive's own sizes; 2.5 was the Stu-fire stand-in's).</summary>
    [Export(PropertyHint.Range, "0.25,4,0.05")] public float SpicyFireScale = 1f;
    /// <summary>153g (Jaysonn: "king boo needs to flash when damage is dealt to him"): on a hit (the flinch and the spicy damage) his body blinks the whole way into
    /// a colour -- DamageFlashTicks long, DamageFlashPeriod ticks on / off -- on top of the game's own speckle (the brk's polmask sweep, which at 150 / 255 is
    /// subtle on a solid Boo). DamageFlashFruitColor: the blink is the fruit's colour (the coconut's pink, the papaya's orange ...); off = DamageFlashColor.
    /// 0 ticks = no flash. 153g: 30 ticks of 6 (half a second); 153h: 90 ("flash longer"); 153j: 150 ticks of 10 -- two and a half seconds, seven and a half
    /// blinks at 3 Hz ("still too short and flickers too fast").</summary>
    [Export(PropertyHint.Range, "0,600,1")] public int DamageFlashTicks = 150;
    [Export(PropertyHint.Range, "1,40,1")] public int DamageFlashPeriod = 10;
    [Export] public bool DamageFlashFruitColor = true;
    [Export] public Color DamageFlashColor = new Color(1f, 0.3f, 0.3f);
    /// <summary>ms_btls_ase: the sweat off his body through the spicy loop -- an SmsNpcEffect scene. Empty = assets/Sms/Fx/NpcDripBody.tscn (x 3) if it exists.</summary>
    [Export] public PackedScene SweatFxScene;
    /// <summary>ms_btls_spicy_hit: the burst at the mouth when the pepper lands (the first 20 frames of bero_hit). Empty = HitBurst.</summary>
    [Export] public PackedScene SpicyHitFxScene;
    /// <summary>The scene's own emitters: ChikaFx (ms_btls_chika_a / b, the twinkle around him), GlowFx (ms_btls_glow, the soft light at his body), DroolMouthFx /
    /// DroolTongueFx (ms_btls_yodare1-3). Off = none of them.</summary>
    [Export] public bool AmbientFx = true;
    [ExportGroup("Hints (the game's balloons 0xE000F / 0xE0010 / 0xE0011 through the HUD's SmsHintBalloon; the lines are not in the rip)")]
    /// <summary>On: the three hints show on the HUD's SmsHintBalloon (none in the scene = nothing).</summary>
    [Export] public bool ShowHints = true;
    /// <summary>0xE000F, once: the first fruit on him while he is NOT spicy (he just eats it).</summary>
    [Export(PropertyHint.MultilineText)] public string HintFirstFruit = "King Boo just gobbled that fruit up! Plain fruit won't hurt him... but did you see how he eyed those red peppers?";
    /// <summary>0xE0010: the third plain fruit in a row (and every one after).</summary>
    [Export(PropertyHint.MultilineText)] public string HintFruitAgain = "He's too happy to eat your fruit! Make him swallow a hot pepper first -- throw it onto his tongue!";
    /// <summary>0xE0011, once: the first fruit hit of all.</summary>
    [Export(PropertyHint.MultilineText)] public string HintFirstHit = "A hit! While his mouth is burning, a fruit on his body really hurts him!";
    [ExportGroup("Camera (params/camera/shakebtelesa*.prm; empty = those tables)")]
    /// <summary>UNK22 shakebtelesaappear (80, 150 / 4000) at the appear clip's frame 78 (never reached: the clip has 59 frames, in the game too); UNK20
    /// shakebtelesahit (150, 80 / 4000, 50 / 300, 100 / 2500) a fruit flinch; UNK1F shakebtelesadamage (80, 80 / 4500, -, 30 / 1000) a spicy hit; UNK21
    /// shakebtelesadown (150, 400 / 4000, 50 / 300, 600 / 3000) the down; UNK23 shakebtelesaroll (50, 120 / 5000) the discs starting.</summary>
    [Export] public SmsCameraShakeMode ShakeLand, ShakeHit, ShakeDamage, ShakeDown, ShakeRoulette;
    /// <summary>153l, TBossTelesa::moveObject's mCameraOffsetY -> gpCamera->unk290: in the room's fixed-point camera (DefiniteD2) the look point sinks toward
    /// Mario's feet while he is within CameraMoveLimitUnits (1000) of the camera's eye -- the offset += CameraMoveSpeed (0.02) x (his feet - the look-at's
    /// height) a tick -- and decays (x 0.02 a tick) when he is further off: standing right under the camera he is still in frame. Off: the plain look point.</summary>
    [Export] public bool CameraChase = true;
    [Export(PropertyHint.Range, "0,4000,10")] public float CameraMoveLimitUnits = 1000f;
    [Export(PropertyHint.Range, "0,0.2,0.005")] public float CameraMoveSpeed = 0.02f;
    /// <summary>SMSRumbleMgr->start(0x14, 0xf): the pad rumbles when the discs start and when he spits a Boo.</summary>
    [Export] public bool Rumble = true;
    [ExportGroup("Sounds (empty = silent)")]
    /// <summary>MSD_SE_BS_TELESA_*: V_LAUGH1 (the laugh), FRUIT_HIT, DOWN, DISAPPEAR, ESCAPE (hiding spicy), THANKYOU (the wet clip), FIRE (spicy), SLT_LAND,
    /// FANFALE_1 (fruit) / _2 (coin) / _3 (else); MSD_SE_EN_TELESA_APPEAR for the attackers is the Boo's own.</summary>
    [Export] public AudioStreamPlayer3D LaughSound, FruitHitSound, DownSound, DisappearSound, EscapeSound, ThankYouSound, FireSound, LandSound, Fanfare1Sound, Fanfare2Sound, Fanfare3Sound;
    /// <summary>The boss BGM (unk384: started at his first appearance; MSBgm::stopBGM at the down): a 2D player with the track in its Stream. Empty = none.</summary>
    [Export] public AudioStreamPlayer BossMusic;

    // ------------------------------------------------------------------------------------------------------------------ the clips (the archive's order)
    private static readonly string[] Names = { "btelesa_appear", "btelesa_bero_hit", "btelesa_damage", "btelesa_down", "btelesa_hide", "btelesa_hit", "btelesa_laugh", "btelesa_lick",
        "btelesa_osenbubbles_birth", "btelesa_osenbubbles_split", "btelesa_osenbubbles_wait", "btelesa_roll", "btelesa_spicy", "btelesa_spicy_end", "btelesa_spit", "btelesa_wait", "btelesa_wet" };
    private static readonly int[] Frames = { 59, 59, 219, 199, 44, 42, 89, 140, 122, 5, 80, 139, 30, 59, 99, 60, 59 };
    private static readonly bool[] Loops = { false, false, false, false, false, false, false, false, false, false, true, false, true, false, false, true, false };
    public const int BckAppear = 0, BckBeroHit = 1, BckDamage = 2, BckDown = 3, BckHide = 4, BckHit = 5, BckLaugh = 6, BckLick = 7, BckOsenBirth = 8, BckOsenSplit = 9, BckOsenWait = 10,
        BckRoll = 11, BckSpicy = 12, BckSpicyEnd = 13, BckSpit = 14, BckWait = 15, BckWet = 16;
    protected override string[] BckNames => Names;
    protected override int[] BckFrames => Frames;
    protected override bool[] BckLoops => Loops;

    // ------------------------------------------------------------------------------------------------------------------ nerves (TNerveBossTelesa*)
    public const int NerveHideWait = NerveSpecies, NerveAppear = NerveSpecies + 1, NerveSlotStart = NerveSpecies + 2, NerveSpitSlotItem = NerveSpecies + 3, NervePrepareSlot = NerveSpecies + 4,
        NerveHide = NerveSpecies + 5, NerveSpit = NerveSpecies + 6, NerveBossFreeze = NerveSpecies + 7, NerveBossDie = NerveSpecies + 8;
    private static readonly string[] BossNerveNames = { "HideWait", "Appear", "SlotStart", "SpitSlotItem", "PrepareSlot", "Hide", "Spit", "BossFreeze", "BossDie" };
    protected override string NerveName(int n) => n >= NerveSpecies && n < NerveSpecies + BossNerveNames.Length ? BossNerveNames[n - NerveSpecies] : base.NerveName(n);
    protected override int DefaultNerve => NerveHideWait;

    // ------------------------------------------------------------------------------------------------------------------ state
    private int _alpha = NormalAlpha, _normalAlpha = NormalAlpha;   // unk34C.a (K0 alpha: the screen mix) and mNormalAlpha (GhostOpacity x 255)
    private float _opacitySeen = -1f;                            // the GhostOpacity last applied: the slider moved while he plays re-derives the alpha (153c)
    private Color _tint = Colors.White;                          // unk348 (K1: the fruit tint)
    private bool _hidden, _spicy, _collisionOn, _slotRolling, _bgmStarted, _marioUnder, _shineDone;   // LIVE_FLAG_HIDDEN / mIsSpicy / the body + tongue filters / unk18C / unk384 / the kill cylinder's unk6C
    private int _spicyTimer, _prepTimer, _dieTimer, _coinRoundsLeft = 3, _slotResult, _fruitHitCount, _fruitHitFx = -1, _brk = 2, _brkTime;   // mSpicyTimer / unk368 / unk388 / unk370 / unk1A8 / mFruitHitCount / mFruitHitEffect / the brk index + its ticks
    private int _btp = 2, _btpTime, _eyeNow = -1, _pauseTicks;   // the face pattern (1 spicy, 2 wait) + its ticks, the eye sheet shown, the result pause (SpitSlotItem)
    private float _maskK;                                        // K2_A: the brk's polmask threshold
    private bool _hintFirstFruit, _hintFirstHit;                 // unk35A / unk35B: the once-only balloons still to show
    private int _intro;                                          // the FallDemo: 0 none / done, 10 armed (the disc raised, waiting for Mario), 1 the disc sinking, 2 the slot falling
    private float _introSlotY;                                   // the falling slot's height, units over the table's centre
    private bool _introHold; private Vector3 _introHoldOffset;   // 153l: Mario TAKEN -- carried at this offset from the sinking disc's origin until the DETACH
    private bool _introLiftPending;                              // the armed ride's lift still to do (on the first physics tick)
    private float _introDiscY;                                   // the centre disc's INTENDED height (global metres): a sync-to-physics body puts a written transform back until its body has moved, so it cannot be read back within the frame
    private int _introTicks;                                     // ticks since the ride began (for the drives)
    private float _camOffY;                                      // mCameraOffsetY (-> the camera's unk290)
    private SmsNpcEffect _spicyFire, _sweat;                     // the spicy loop's bound emitters (ms_btls_spicy_a..d, ms_btls_ase)
    private readonly List<SmsNpcEffect> _drool = new(); private bool _droolOn;   // the strands (yodare1 / 2 at the mouth, yodare3 at the tongue tip)
    private float _slotShownY; private bool _slotShownKnown;     // 153c: the slot machine's shown height, units, chasing the game's placement
    private float _slotTargetPrev;                               // 153h: the game's placement last tick (the SlotLagUnits reader)
    private float _slotSeatPrev; private int _slotAnchorJoint = -1;   // 153j: the machine's seat last tick (the anchor joint - SlotUnderUnits, no hosei, no dip) and which joint -- his clips' motion the machine rides
    private GpuParticles3D _chikaFx; private Node3D _glowFx;     // the scene's own emitters
    private SmsHintBalloon _hint; private bool _hintLooked;
    private float _hoseiY;                                       // mHoseiPosY (the dip; unclamped -- the slot rides it)
    private float _floorY;                                       // mGroundHeight: the table under him, units (his own: the base's _groundY is its own ray)
    private float _prevBckFrame; private int _prevBck = -1;
    private readonly List<Node3D> _spit = new();                 // mSpitObjs
    private readonly HashSet<Fruit> _spitFruitNoCollision = new();   // the spat fruit still NO_COLLISION (before fruitCollisionOn)
    private readonly List<SmsTelesa> _attackers = new();         // the stage's TTelesaManager objects he borrows
    private readonly float[] _rouletteSp = new float[3];         // the slot's unk1E4: the disc speeds he set
    private readonly List<ShaderMaterial> _mats = new(); private ShaderMaterial _bodyMat;
    private Skeleton3D _skel; private int _bBody = -1, _bMouth = -1, _bTongue = -1, _bTongueTip = -1, _bSpicy = -1;
    private SmsBossTelesaPart _bodyPart, _tonguePart;
    private readonly SmsRoulette[] _roulettes = new SmsRoulette[3];
    /// <summary>For the drives.</summary>
    public int Alpha => _alpha;
    public float FadeNow => _alpha / 255f;
    public float MaskK => _maskK;
    public int Btp => _btp;
    public int EyeSheet => _eyeNow;                              // 0 open, 1 half, 2 close, 3 roll
    public int IntroPhase => _intro;
    public bool IntroHoldsMario => _introHold;
    public int IntroTicks => _introTicks;
    public float CameraOffsetY => _camOffY;
    public int ResultPause => _pauseTicks;
    /// <summary>The slot machine is out of sight under the table: hidden, or its cabinet's top (642 u over its origin) under his floor -- the pressed bowl's
    /// bottom, so under the whole table. The spit waits for this (153g).</summary>
    public bool SlotSunk => Slot == null || !IsInstanceValid(Slot) || !Slot.Visible || !_slotShownKnown || _slotShownY + SlotCabinetTopUnits <= _floorY;
    public const float SlotCabinetTopUnits = 642f;              // srot_waku.glb: y -30..642 from its origin (slot_geo153.py)
    /// <summary>How far the shown machine is UNDER the game's placement of it, units (0 = riding it exactly; the sink's lag is negative).</summary>
    public float SlotLagUnits => Slot != null && IsInstanceValid(Slot) && Slot.Visible && _slotShownKnown ? _slotTargetPrev - _slotShownY : 0f;
    /// <summary>The machine's shown height and its seat under him (joint 0 - SlotUnderUnits), units; the dip his model wears (the hosei's, held to the
    /// machine's sink since 153j).</summary>
    public float SlotShownUnits => _slotShownY;
    public float SlotHomeUnits => _slotHome;
    public float ModelDipUnits => _modelDip;
    private float _modelDip, _slotHome;
    public float HoverUnits => HoverHeight * U;                  // the model's origin over the table's centre floor (the game's 300 + 800)
    public float TransYUnits => HoverHeight * U - PositionOverFloor;
    public bool SpicyFireOn => _spicyFire != null && IsInstanceValid(_spicyFire) && _spicyFire.IsEmitting;
    public string SpicyFireSceneName => _spicyFire != null && IsInstanceValid(_spicyFire) ? _spicyFire.SceneFilePath.GetFile() : "";
    /// <summary>The body material's `mask_k` as set (1 while a damage flash is on; the brk's value otherwise).</summary>
    public float BodyMaskParam => _bodyMat != null ? (float)_bodyMat.GetShaderParameter("mask_k") : -1f;
    public Color Tint => _tint;
    public bool IsHidden => _hidden;
    public bool IsSpicy => _spicy;
    public bool CollisionOn => _collisionOn;
    public bool SlotRolling => _slotRolling;
    public bool MarioUnder => _marioUnder;
    public float HoseiY => _hoseiY;
    public float FloorYUnits => _floorY;
    public int SlotResultNow => _slotResult;
    public int PrepTimer => _prepTimer;
    public int CoinRoundsLeft => _coinRoundsLeft;
    public int SpitCount => _spit.Count;
    public int AttackersAlive { get { int n = 0; foreach (var a in _attackers) if (a != null && IsInstanceValid(a) && !a.IsDead) n++; return n; } }
    public bool ShineSpawned => _shineDone;
    public IReadOnlyList<Node3D> SpitObjects => _spit;
    public IReadOnlyList<SmsTelesa> Attackers => _attackers;
    public SmsRoulette RouletteAt(int i) => i >= 0 && i < 3 ? _roulettes[i] : null;
    public Vector3 BodyPointUnits => JointUnits(_bBody) + Vector3.Down * BodyHitDrop;
    public Vector3 TonguePointUnits => JointUnits(_bTongue) + Vector3.Down * TongueHitDrop;
    public Vector3 MouthUnits => JointUnits(_bMouth);
    public Vector3 KillPointUnits { get { Vector3 j = JointUnits(_bBody); return new Vector3(j.X, Pos().Y - KillDrop, j.Z); } }
    public new string DebugState => $"{NerveNameNow} t{NerveTime} bck {BckName} f{BckFrame():F0} hp {_hp} alpha {_alpha} spicy {_spicy} hidden {_hidden} col {_collisionOn} hosei {_hoseiY:F0} slot {(Slot != null ? (Slot.IsRolling ? "rolling" : "still") : "-")} result {_slotResult} prep {_prepTimer} spit {_spit.Count} boos {AttackersAlive} under {_marioUnder} y {Pos().Y:F0} floor {_floorY:F0}";

    // ------------------------------------------------------------------------------------------------------------------ setup
    protected override bool FlyBind => true;

    /// <summary>TBossTelesa::init: the model's materials copied per body (the fade and the tint are his own), the joints, the two spray parts, the arena.</summary>
    protected override void Init()
    {
        _noBind = true;
        ShineScene ??= LoadIf("res://shine_sprite.tscn");
        CoconutScene ??= LoadIf("res://fruit_coconut.tscn"); PapayaScene ??= LoadIf("res://fruitpapaya.tscn"); PineappleScene ??= LoadIf("res://fruit_pineapple.tscn");
        DurianScene ??= LoadIf("res://fruit_durian.tscn"); RedPepperScene ??= LoadIf("res://fruit_red_pepper.tscn"); CoinScene ??= LoadIf("res://yellow_coin.tscn");
        BooScene ??= LoadIf("res://assets/Sms/Enemy/Telesa.tscn");
        if (SlotEnemyScenes == null || SlotEnemyScenes.Count == 0)
        {
            string[] defaults = { null, "res://assets/Sms/Enemy/Hamukuri.tscn", "res://assets/Sms/Enemy/FireHamukuri.tscn", "res://assets/Sms/Enemy/Bomb/BombHei.tscn", "res://assets/Sms/Enemy/Poihana.tscn",
                null, null, "res://assets/Sms/Enemy/MameGesso.tscn", "res://assets/Sms/Enemy/Puku.tscn" };
            SlotEnemyScenes = new Godot.Collections.Array<PackedScene>();
            foreach (string p in defaults) SlotEnemyScenes.Add(p != null ? LoadIf(p) : null);
        }
        FruitHitFxScene ??= LoadIf("res://assets/Sms/Fx/HitBurst.tscn");
        FruitPuffFxScene ??= LoadIf("res://assets/Sms/Fx/EnemyDisap.tscn");
        SpicyHitFxScene ??= LoadIf("res://assets/Sms/Fx/HitBurst.tscn");
        DamageFxScene ??= LoadIf("res://assets/Sms/Fx/EnemyDisap.tscn");
        DownFxScene ??= LoadIf("res://assets/Sms/Fx/EnemyDisap.tscn");
        ItemKillFxScene ??= LoadIf("res://assets/Sms/Fx/EnemyDisap.tscn");
        FanfareFxScene ??= LoadIf("res://assets/Sms/Enemy/BossTelesa/BtlsFubuki.tscn");
        SpicyFireFxScene ??= LoadIf("res://assets/Sms/Fx/BtlsSpicyFire.tscn") ?? LoadIf("res://assets/Sms/Fx/StuFire.tscn");
        SweatFxScene ??= LoadIf("res://assets/Sms/Fx/NpcDripBody.tscn");
        if (DroolFxScene == null && DroolSmallFxScene == null) { DroolFxScene = LoadIf("res://assets/Sms/Fx/GkYodareL.tscn"); DroolSmallFxScene = LoadIf("res://assets/Sms/Fx/GkYodareS.tscn"); }
        EyeOpen ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_open.png");
        EyeHalf ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_half.png");
        EyeClose ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_close.png");
        EyeRoll ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_roll.png");
        // params/camera/shakebtelesa{appear,hit,damage,down,roll}.prm: mShakeTime, mShakeAmpX / VelX, AmpY / VelY, AmpZ / VelZ
        ShakeLand ??= new SmsCameraShakeMode { TimeTicks = 80, AmpX = 150f, VelX = 4000 };
        ShakeHit ??= new SmsCameraShakeMode { TimeTicks = 150, AmpX = 80f, VelX = 4000, AmpY = 50f, VelY = 300, AmpZ = 100f, VelZ = 2500 };
        ShakeDamage ??= new SmsCameraShakeMode { TimeTicks = 80, AmpX = 80f, VelX = 4500, AmpZ = 30f, VelZ = 1000 };
        ShakeDown ??= new SmsCameraShakeMode { TimeTicks = 150, AmpX = 400f, VelX = 4000, AmpY = 50f, VelY = 300, AmpZ = 600f, VelZ = 3000 };
        ShakeRoulette ??= new SmsCameraShakeMode { TimeTicks = 50, AmpX = 120f, VelX = 5000 };
        _chikaFx = GetNodeOrNull<GpuParticles3D>("ChikaFx"); _glowFx = GetNodeOrNull<Node3D>("GlowFx");
        foreach (Node3D fx in new Node3D[] { _chikaFx, _glowFx }) if (fx != null) { fx.TopLevel = true; fx.Visible = false; }
        _mats.Clear(); _bodyMat = null;
        if (_model != null)
        {
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    if (mi.GetActiveMaterial(i) is not ShaderMaterial sm) continue;
                    var copy = (ShaderMaterial)sm.Duplicate();
                    mi.SetSurfaceOverrideMaterial(i, copy);
                    _mats.Add(copy);
                    if ((sm.ResourceName ?? "").StartsWith("_mat_body") || (copy.ResourceName ?? "").StartsWith("_mat_body") || _bodyMat == null) _bodyMat = copy;
                }
            }
            foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; break; }
            if (_skel != null)
            {
                _bBody = _skel.FindBone("jnt_body"); _bMouth = _skel.FindBone("jnt_mouth"); _bTongue = _skel.FindBone("jnt_sita_2"); _bTongueTip = _skel.FindBone("eff_sita"); _bSpicy = _skel.FindBone("jnt_sita_4");
                if (_bBody < 0 && _skel.GetBoneCount() > 1) _bBody = 1;
                if (_bMouth < 0 && _skel.GetBoneCount() > 5) _bMouth = 5;
                if (_bTongue < 0 && _skel.GetBoneCount() > 7) _bTongue = 7;
            }
        }
        foreach (Node n in GetChildren()) if (n is CollisionShape3D cs) cs.Disabled = true;   // the game's own cylinder is a hit check (below), not a wall
        _bodyPart = new SmsBossTelesaPart { Name = "BodyPart" }; AddChild(_bodyPart); _bodyPart.Setup(this, SmsBossTelesaPart.Body, BodyHitRadius, BodyHitHeight);
        _tonguePart = new SmsBossTelesaPart { Name = "TonguePart" }; AddChild(_tonguePart); _tonguePart.Setup(this, SmsBossTelesaPart.Tongue, TongueHitRadius, TongueHitHeight);
        Slot ??= GetNodeOrNull<SmsTelesaSlot>("Slot");
        if (Slot != null) { Slot.Boss = this; Slot.TopLevel = true; }
        CallDeferred(nameof(FindArena));
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: joints body {_bBody} mouth {_bMouth} tongue {_bTongue}; {_mats.Count} materials; slot {(Slot != null ? Slot.Name : "none")}");
    }
    private static PackedScene LoadIf(string path) => ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
    private static Texture2D LoadTex(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    /// <summary>loadAfter: the ACTOR_TYPE_CASINORULET objects -- the exports, else the `sms_roulette` group highest first (rulet00 the top disc).</summary>
    private void FindArena()
    {
        _roulettes[0] = RouletteTop; _roulettes[1] = RouletteMiddle; _roulettes[2] = RouletteBottom;
        if (_roulettes[0] == null || _roulettes[1] == null || _roulettes[2] == null)
        {
            // 153b: HIS table only -- the discs within 60 m across and 30 m up or down of him (two arenas in one level: each boss takes its own)
            var found = new List<SmsRoulette>();
            Vector3 me = IsInsideTree() ? GlobalPosition : Vector3.Zero;
            foreach (Node n in GetTree().GetNodesInGroup("sms_roulette"))
            {
                if (n is not SmsRoulette r || !r.IsInsideTree()) continue;
                Vector3 d = r.GlobalPosition - me; float dy = MathF.Abs(d.Y); d.Y = 0f;
                if (d.Length() < 60f && dy < 30f) found.Add(r);
            }
            found.Sort((a, b) => a.RadiusUnits.CompareTo(b.RadiusUnits));   // rulet00 is the small centre disc, rulet02 the outer ring
            for (int i = 0; i < 3; i++) if (_roulettes[i] == null && i < found.Count) _roulettes[i] = found[i];
        }
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: discs {(_roulettes[0]?.Name ?? "-")} / {(_roulettes[1]?.Name ?? "-")} / {(_roulettes[2]?.Name ?? "-")}");
    }

    /// <summary>TBossTelesa::reset: NO_COLLISION, HIDDEN, the spine at HideWait; the alpha the normal ghost's, the tint white, the slot hidden.</summary>
    protected override void Reset()
    {
        base.Reset();
        _noBind = true;
        MotionMode = MotionModeEnum.Floating;
        _hp = Math.Max(1, HitPoints);
        _bodyScale = 1f; _scale = 1f;
        _normalAlpha = Math.Clamp(Mathf.RoundToInt(GhostOpacity * 255f), 1, 0xFE); _opacitySeen = GhostOpacity;
        _alpha = _normalAlpha; _tint = Colors.White; _brk = 2; _brkTime = 0; _maskK = 0f; _btp = 2; _btpTime = 0; _eyeNow = -1; _pauseTicks = 0;
        _spicy = false; _slotRolling = false; _marioUnder = false; _shineDone = false; _bgmStarted = false;
        _hintFirstFruit = true; _hintFirstHit = true;
        _spicyTimer = 0; _prepTimer = 0; _dieTimer = 0; _coinRoundsLeft = CoinRounds; _slotResult = SmsTelesaSlot.Coin; _fruitHitCount = 0; _fruitHitFx = -1;
        _hoseiY = 0f; _prevBck = -1; _prevBckFrame = 0f;
        _yaw = _spawnYaw;
        for (int i = 0; i < 3; i++) _rouletteSp[i] = 0f;
        _spit.Clear(); _spitFruitNoCollision.Clear();
        StopSpicyFx(); StopDrool(); _droolOn = false; _slotShownKnown = false; _slotAnchorJoint = -1;
        SetHidden(StartHidden || IntroRide);
        OffAllCollision();
        if (Slot != null) { Slot.Visible = false; Slot.ForcedResult = ForcedResultValue(); }
        ApplyAlpha(true); ApplyTint(); ApplyMask(); ApplyEye(0);
        if (IsInsideTree()) { _floorY = GroundYMap(); SetY(_floorY + PositionOverFloor); }
        _intro = 0; _introHold = false; _introLiftPending = false; _introTicks = 0; _camOffY = 0f;
        if (IntroRide && IsInsideTree()) CallDeferred(nameof(IntroArm));
        ApplyLook();
    }
    /// <summary>The FallDemo armed (deferred: the discs are found a frame late): the centre disc lifted IntroLiftUnits and left there, the slot out of sight;
    /// the ride itself begins at IntroStart's moment (IntroBegin).</summary>
    private void IntroArm()
    {
        if (!IntroRide || _intro != 0 || _dead) return;
        var disc = _roulettes[0];
        if (disc == null || !IsInstanceValid(disc)) { FindArena(); disc = _roulettes[0]; }
        if (disc == null || !IsInstanceValid(disc)) { if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: no centre disc for the intro ride"); return; }
        _intro = 10; _introTicks = 0; _introLiftPending = true;   // the lift itself on the first tick: a sync-to-physics disc moved OUTSIDE the physics step (this deferred call runs after it) is put back by the next frame's state write-back before its body has moved
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: the intro ride armed: {disc.Name} to be lifted {IntroLiftUnits:F0} u, waiting ({IntroStart})");
    }
    /// <summary>The disc raised (TRoulette's placement 2000 u up), inside the physics tick so its body takes the move.</summary>
    private void IntroLift(SmsRoulette disc)
    {
        _introLiftPending = false;
        _introDiscY = disc.GlobalPosition.Y + IntroLiftUnits / U;
        disc.GlobalPosition = DiscPos(disc);
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: the intro ride: {disc.Name} lifted {IntroLiftUnits:F0} u to {DiscPos(disc)} (its parent at {(disc.GetParent() as Node3D)?.GlobalPosition})");
        if (IntroStart == IntroStarts.AtOnce) IntroBegin(disc, false);
    }
    /// <summary>The centre disc's place as intended this tick (its node reads a frame behind: AnimatableBody3D's sync-to-physics write-back).</summary>
    private Vector3 DiscPos(SmsRoulette disc) => new Vector3(disc.GlobalPosition.X, _introDiscY, disc.GlobalPosition.Z);
    /// <summary>TNerveBossTelesaFallDemo's first tick: HIDDEN, Mario TAKEN (SMS_SendMessageToMario(HIT_MESSAGE_TAKE): the boss carries him -- here onto the raised
    /// disc's centre when the room's edge started the ride, or held where he stands on it), the clips frozen, the slot's scale 0; and the game's init-time
    /// fireStartDemoCamera("btelesa_roll_camera"): the intro camera from here, in the table's frame (the discs' origins = the stage's y 500).</summary>
    private void IntroBegin(SmsRoulette disc, bool take)
    {
        _intro = 1; _introTicks = 0;
        if (_mario != null && IsInstanceValid(_mario))
        {
            _mario.CameraLocked = true;
            if (take)
            {
                // the TAKE: onto the disc's centre, its pressed pond's bottom (PondYUnits under the disc's origin), facing the way he came
                _introHoldOffset = new Vector3(0f, disc.PondIsBowl ? disc.PondYUnits / U : (disc.FaceYUnits / U - disc.GlobalPosition.Y), 0f);   // the offsets read off the node are the disc's own shape: a frame's lag is nothing
            }
            else _introHoldOffset = _mario.GlobalPosition - DiscPos(disc);
            _mario.SmsCarryTo(DiscPos(disc) + _introHoldOffset);   // the TAKE: off whatever deck he rode (SmsRideApply would put him back on it)
            _introHold = true;
            var cam = _mario.SmsCamera;
            if (IntroCamera && cam != null && IntroCameraPath != null && IntroCameraPath.IsValid)
                cam.StartDemoCamera(IntroCameraPath, StageFrame());
            else if (IntroCamera && DebugPrint) GD.Print($"[SMS btelesa] {Name}: no intro camera path / camera");
        }
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: the intro ride begins: Mario {(take ? "taken onto" : "riding")} {disc.Name} at {DiscPos(disc)} ({(_roulettes[1] != null ? (_introDiscY - _roulettes[1].GlobalPosition.Y) * U : 0f):F0} u over the middle ring); {IntroLiftUnits / IntroFallSpeed / SmsUnits.TicksPerSecond:F1} s down; Mario at {_mario?.GlobalPosition}");
    }
    /// <summary>The stage's frame in the level: the stage's origin is 500 u under the discs' origins (TRoulette::initMapObj's lift), with the table's own
    /// bearing; the camera paths and the room's data are in it.</summary>
    private Transform3D StageFrame()
    {
        var anchor = _roulettes[1] ?? _roulettes[2] ?? _roulettes[0];
        Basis b = anchor?.GetParent() is Node3D p ? p.GlobalBasis.Orthonormalized() : Basis.Identity;
        Vector3 o = anchor != null && IsInstanceValid(anchor) ? anchor.GlobalPosition : GlobalPosition;
        return new Transform3D(b, o - b * new Vector3(0f, 500f / U, 0f));
    }
    /// <summary>The armed ride's trigger each tick: WhenMarioEntersRoom -- within IntroTriggerRadiusUnits of the table's centre and not down the pit;
    /// WhenMarioLandsOnDisc -- standing on the raised disc.</summary>
    private void IntroArmedTick(SmsRoulette disc)
    {
        if (_mario == null || !IsInstanceValid(_mario) || !_mario.IsInsideTree()) return;
        Vector3 m = _mario.GlobalPosition, c = disc.GlobalPosition;
        float r = new Vector2(m.X - c.X, m.Z - c.Z).Length() * U;
        if (IntroStart == IntroStarts.WhenMarioEntersRoom)
        {
            if (r < IntroTriggerRadiusUnits && m.Y * U > TableSurfaceUnits - 1000f && m.Y * U < disc.FaceYUnits + 3000f) IntroBegin(disc, true);
        }
        else if (IntroStart == IntroStarts.WhenMarioLandsOnDisc)
        {
            if (r < disc.RadiusUnits && _mario.IsOnFloor() && MathF.Abs(m.Y * U - disc.FaceYUnits) < 300f) IntroBegin(disc, false);
        }
    }
    /// <summary>TNerveBossTelesaFallDemo, one tick: rouletteFall (the centre disc down IntroFallSpeed u a tick to the others' height, Mario carried on it until
    /// 3 u from the bottom -- the DETACH, RLT_SET; V_LAUGH1B / RLT_DOWN), then slotFall (the machine, unseen, at 5 u a tick, 1 u the last 300; passing 200 u
    /// over its place the discs start turning; home -> the fight).</summary>
    private void IntroTick()
    {
        var disc = _roulettes[0]; var other = _roulettes[1] ?? _roulettes[2];
        if (disc == null || !IsInstanceValid(disc)) { IntroEnd(); return; }
        if (_intro == 10) { if (_introLiftPending) IntroLift(disc); if (_intro == 10) IntroArmedTick(disc); return; }
        _introTicks++;
        if (_intro == 1)
        {
            float target = other != null && IsInstanceValid(other) ? other.GlobalPosition.Y : _introDiscY - IntroLiftUnits / U;
            float left = (_introDiscY - target) * U;
            if (left > 0.5f)
            {
                _introDiscY -= MathF.Min(IntroFallSpeed, left) / U;
                disc.GlobalPosition = DiscPos(disc);
                if (_introHold && left > 3f && _mario != null && IsInstanceValid(_mario)) _mario.SmsCarryTo(DiscPos(disc) + _introHoldOffset);
                else if (_introHold) { _introHold = false; if (DisappearSound != null && DisappearSound.Stream != null) DisappearSound.Play(); }   // the DETACH 3 u up: MSD_SE_BS_TELESA_RLT_SET stands in
                return;
            }
            _introDiscY = target;
            disc.GlobalPosition = DiscPos(disc);
            if (_introHold) { _introHold = false; if (DisappearSound != null && DisappearSound.Stream != null) DisappearSound.Play(); }
            _intro = 2; _introSlotY = TransYUnits - SlotUnderUnits + 1500f;
            if (Slot != null) Slot.Visible = IntroSlotShown;
            IntroRelease();   // 153m: the landing ends the demo and frees him (the game: the bck ran the fall's length; slotFall holds nothing)
            return;
        }
        float home = TransYUnits - SlotUnderUnits;            // where calcRootMatrix hangs it (joint 0 - 700 in the game; SlotUnderUnits here, no hosei)
        float above = _introSlotY - home;
        _introSlotY -= above > 300f ? 5f : 1f;
        if (above <= 200f && above + 5f > 200f) RouletteStart();   // slotFall: the discs start as it passes
        if (Slot != null && IsInstanceValid(Slot))
        {
            Vector3 c = IsInsideTree() ? GlobalPosition : Vector3.Zero;
            Slot.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(_yaw)), new Vector3(c.X, (_floorY + _introSlotY) / U, c.Z));
        }
        if (_introSlotY <= home) IntroEnd();
    }
    /// <summary>The disc down: the game's demo camera has run the fall's length (the director ends it; the cube's fixed camera takes the view in a cut) and
    /// rouletteFall's onNeutralMarioKey stops with the fall -- Mario is in control through the slot's unseen fall. 153m (Jaysonn: "mario should be in
    /// control once the camera goes to the normal spot for the fight").</summary>
    private void IntroRelease()
    {
        if (_mario == null || !IsInstanceValid(_mario)) return;
        _mario.CameraLocked = false;
        _mario.SmsCamera?.EndDemoCamera();               // the game's demo is the bck's length (= the fall at 1 u a tick); a slower fall ends it here at the latest
    }
    private void IntroEnd()
    {
        _intro = 0; _introHold = false;
        IntroRelease();
        if (Slot != null) Slot.Visible = false;              // HideWait hides it until the appear
        _time = 0;                                           // HideWait's clock starts here
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: the intro ride over after {_introTicks} ticks -> HideWait");
    }
    /// <summary>TBossTelesa::moveObject's camera chase (mCameraMoveLimit 1000, mCameraMoveSp 0.02): his mCameraOffsetY onto the camera's unk290 -- read only
    /// in the room's DefiniteD2 kind (setMarioLookat_). The game writes it every tick he lives, dead or not shown; so here.</summary>
    private void CameraChaseTick()
    {
        if (_mario == null || !IsInstanceValid(_mario)) return;
        var cam = _mario.SmsCamera; if (cam == null) return;
        if (!CameraChase) { if (_camOffY != 0f) { _camOffY = 0f; cam.DefiniteAtOffsetY = 0f; } return; }
        Vector3 marioU = _mario.GlobalPosition * U;
        float dist = (marioU - cam.ViewEyeUnits).Length();
        if (dist < CameraMoveLimitUnits) _camOffY += CameraMoveSpeed * (marioU.Y - cam.ViewAtUnits.Y);
        else if (MathF.Abs(_camOffY) > 1f) _camOffY *= CameraMoveSpeed;
        cam.DefiniteAtOffsetY = _camOffY;
    }
    private int ForcedResultValue() => ForceResult switch { SlotForce.Coin => SmsTelesaSlot.Coin, SlotForce.Enemy => SmsTelesaSlot.Enemy, SlotForce.Fruit => SmsTelesaSlot.Fruit, SlotForce.Jackpot => SmsTelesaSlot.Jackpot, _ => int.MinValue };
    /// <summary>makeShineAppearWithDemo is the end: the body is freed with its slot (RelaunchTicks -1 in the scene); a relaunch would start the fight over.</summary>
    protected override void LaunchVelocity() { }
    protected override void MoveObjectPre()
    {
        _noBind = true;
        _floorY = GroundYMap();
        SetY(_floorY + PositionOverFloor);                       // moveObject: mPosition.y = mGroundHeight + 300
        if (Slot != null && !_dead) Slot.TickSlot(HitCollectRate, _rng);   // TTelesaSlot::moveObject (the slot is his to run)
        if (_intro != 0 && !_dead) IntroTick();
        if (!_dead) CameraChaseTick();                           // moveObject: mCameraOffsetY -> gpCamera->unk290 (153l)
    }
    /// <summary>The floor under him, MAP bodies only (the discs; never a fruit, an enemy or Mario), units -- within 10 m under his point (he hangs 4 m over the
    /// table: a floor further down is the room's, not his); none found: where he was (the discs' shapes are a frame late on the first tick).</summary>
    private float GroundYMap()
    {
        var space = GetWorld3D().DirectSpaceState;
        Vector3 from = GlobalPosition + Vector3.Up * (P.HeadHeight / U);
        var q = PhysicsRayQueryParameters3D.Create(from, GlobalPosition + Vector3.Down * 10f, CollisionMask);
        var excl = new Godot.Collections.Array<Rid> { GetRid() };
        if (_mario != null && IsInstanceValid(_mario)) excl.Add(_mario.GetRid());
        for (int guard = 0; guard < 6; guard++)
        {
            q.Exclude = excl;
            var hit = space.IntersectRay(q);
            if (hit.Count == 0) break;
            var col = hit["collider"].AsGodotObject();
            if (SmsCoinItem.IsMap(col)) return ((Vector3)hit["position"]).Y * U;
            if (col is CollisionObject3D co) excl.Add(co.GetRid()); else break;
        }
        return Pos().Y - PositionOverFloor;                      // no map floor within reach: stay
    }
    private void SetY(float yUnits) => GlobalPosition = new Vector3(GlobalPosition.X, yUnits / U, GlobalPosition.Z);
    private Vector3 JointUnits(int bone)
    {
        if (_skel == null || bone < 0) return Pos() + Vector3.Up * TransYUnits;
        return (_skel.GlobalTransform * _skel.GetBoneGlobalPose(bone).Origin) * U;
    }
    protected override float GravityY() => 0f;
    protected override bool IsFindMario(float k) => false;
    protected override bool IsCollidMove(Node3D other) => false;
    protected override bool WallHopAllowed() => false;
    protected override Mario.SmsDamageProfile DamageProfile => Mario.SmsDamageProfile.EnemyCommon;

    // ------------------------------------------------------------------------------------------------------------------ the look
    /// <summary>calcRootMatrix: the model at his point + TransYOffset + the hosei dip (clamped -300..0), his yaw; the slot at joint 1 (- 700 through the appear
    /// clip, - 2400 through the hide / damage / hit clips) or joint 0 (- 700, the wait) + the UNCLAMPED hosei, turned with him; the parts at their joints.</summary>
    protected override void ApplyLook()
    {
        if (_model == null) return;
        float home = _floorY + PositionOverFloor + TransYUnits - SlotUnderUnits;   // the machine's seat under him at rest (joint 0 - SlotUnderUnits)
        // 153j: the hosei dips his model only as far as the machine has SUNK under its seat -- he rides it down and it drops away under him (the game dipped
        // him -300 in three ticks while its slot fell out of sight at 100 u a tick; with 153c's visible sink he sank 4 m into a cabinet still in plain view)
        bool slotLive = Slot != null && IsInstanceValid(Slot) && Slot.Visible && _slotShownKnown && _intro != 2 && IsInsideTree();
        float dip = Mathf.Clamp(slotLive ? Mathf.Max(_hoseiY, _slotShownY - _slotSeatPrev) : _hoseiY, BaseHoseiPosY, 0f);   // against the SEAT (his clips' motion without the dip), not the static home: the wait clip's bob is in both and cancels
        _modelDip = dip; _slotHome = home;
        Basis b = new Basis(Vector3.Up, Mathf.DegToRad(_yaw)) * Basis.FromScale(Vector3.One * _scale * _modelScale);
        Vector3 at = IsInsideTree() ? GlobalPosition : Vector3.Zero;
        at += Vector3.Up * ((TransYUnits + dip) / U);
        if (IsInsideTree()) _model.GlobalTransform = new Transform3D(b, at);
        else _model.Transform = new Transform3D(b, at);
        if (!IsInsideTree() || Engine.IsEditorHint()) return;
        if (Slot != null && IsInstanceValid(Slot) && _intro != 2)
        {
            float slotOff = -SlotUnderUnits;                  // the game's -700 (153e: a knob -- the cabinet is 642 u tall, 700 sinks his body into it)
            Vector3 anchor; int anchorJoint;
            if (IsBck(BckHide) || IsBck(BckAppear) || IsBck(BckDamage) || IsBck(BckHit))
            {
                anchor = JointUnits(_bBody); anchorJoint = 1;
                if (!IsBck(BckAppear)) slotOff = -2400f - (SlotUnderUnits - 700f);
            }
            else { anchor = _skel != null ? JointUnits(0) : Pos() + Vector3.Up * TransYUnits; anchorJoint = 0; }
            // 153c: the game's placement (the unclamped hosei carries it 100 u a tick out of sight; the hide / damage / hit clips park it 2400 u under the body)
            // is the TARGET; the machine shown chases it -- down at SlotSinkSpeed (a visible sink into the table), up at SlotRiseSpeed -- and never deeper
            // than SlotHideDepthUnits under his floor, so it is back under him by the appear
            float parked = _floorY - SlotHideDepthUnits;
            float target = Mathf.Max(_hoseiY + anchor.Y + slotOff, parked);   // `home` (the seat, joint 0 - SlotUnderUnits): computed above for his dip; at or above it the machine rides him rigidly
            // 153h/j: his BODY's own motion this tick rides along, both ways -- the appear clip carries jnt_body from 1293 u under his rest to 826 over it in
            // some 30 ticks (153h: a chase capped at SlotRiseSpeed fell behind him and popped into place at the clip's end), the hide clip hops it up and dives
            // it 1771 u in 88 ticks (153j: the machine lingered 2.7 s over an empty table) -- so the machine is bolted under him through his clips, as the
            // game's bound one is, and only what the HOSEI does (the spit's drop-away, the park) is chased at SlotSinkSpeed / SlotRiseSpeed. The ride is
            // read on one joint at a time (the anchor swaps between jnt_body and the root, 478 u apart at the hide's first frame) and not while it is parked
            // under the table (the damage clip bounces the root; a parked machine riding that would ratchet upward).
            // The ride and the seat are read WITHOUT his dip: the dip follows the machine's sink (above), and a machine riding its own effect on him would run
            // away from him at twice the sink speed (run aa: 44 u of error, the spit 150 ticks early).
            float seat = anchor.Y - dip + slotOff;
            float ride = _slotShownKnown && anchorJoint == _slotAnchorJoint && _slotShownY > parked + 1f ? seat - _slotSeatPrev : 0f;
            _slotSeatPrev = seat; _slotAnchorJoint = anchorJoint; _slotTargetPrev = target;
            if (!_slotShownKnown || !Slot.Visible || target >= home - 1f) { _slotShownY = target; _slotShownKnown = true; }
            else
            {
                float s = _slotShownY + ride;
                _slotShownY = target < s ? Mathf.Max(target, s - SlotSinkSpeed) : Mathf.Min(target, s + SlotRiseSpeed);
            }
            Vector3 sp = new Vector3(anchor.X, _slotShownY, anchor.Z) / U;
            Slot.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(_yaw)), sp);
        }
        _bodyPart?.Place(BodyPointUnits / U);
        _tonguePart?.Place(TonguePointUnits / U);
        PlaceAmbientFx();
    }
    /// <summary>calcRootMatrix's emits: chika_a / b at his origin and the glow at joint 1 while he shows (bound to him: they ride the body); the drool at the mouth
    /// (yodare1 / 2) and the tongue tip (yodare3) but through the hide (4), laugh (6), spicy (12) and spicy_end (13) clips. Off while hidden / dead.</summary>
    private void PlaceAmbientFx()
    {
        bool show = AmbientFx && !_hidden && !_dead && _model != null && _model.Visible && _intro == 0;
        float k = _alpha / 255f;
        if (_chikaFx != null)
        {
            _chikaFx.Visible = show; _chikaFx.Emitting = show;
            if (show) _chikaFx.GlobalPosition = JointUnits(_bBody) / U;
        }
        if (_glowFx != null)
        {
            _glowFx.Visible = show;
            if (show)
            {
                _glowFx.GlobalPosition = JointUnits(_bBody) / U;
                if (_glowFx is MeshInstance3D gm && gm.GetActiveMaterial(0) is ShaderMaterial gsm) gsm.SetShaderParameter("strength", k);
            }
        }
        bool drool = DroolOn && show && !IsBck(BckHide) && !IsBck(BckLaugh) && !IsBck(BckSpicy) && !IsBck(BckSpicyEnd);
        if (drool != _droolOn) { _droolOn = drool; if (drool) StartDrool(); else StopDrool(); }
    }
    /// <summary>ms_btls_yodare1 / 2 (emitAndBindToMtxPtr at joint 5, the mouth) and yodare3 (joint 10, the tongue tip): the Piranha's strand scenes bound to the
    /// joints (SmsNpcEffect.Spawn), recoloured to spit, their ribbons at this boss's length and width.</summary>
    private void StartDrool()
    {
        if (_skel == null) return;
        foreach (var (scene, joint, side) in new[] { (DroolFxScene, "jnt_mouth", 1f), (DroolFxScene, "jnt_mouth", -1f), (DroolSmallFxScene, _bTongueTip >= 0 ? "eff_sita" : "jnt_sita_2", 0f) })
        {
            if (scene == null) continue;
            var fx = SmsNpcEffect.Spawn(scene, _skel, joint, 1f, _fxCache, Name + joint + (side > 0f ? "R" : side < 0f ? "L" : ""));
            if (fx == null) continue;
            fx.SetGoopColors(DroolLight, DroolDark);
            foreach (Node c in fx.GetChildren())
                if (c is SmsGoopStrand strand)
                {
                    strand.LengthScale = DroolLength; strand.WidthScale = DroolWidth; strand.TipTaper = DroolRoundTip; strand.FollowAnchor = DroolFollow; strand.FaceCamera = DroolFaceCamera;
                    if (side != 0f) strand.AnchorOffsetUnits = new Vector3(DroolLipUnits, 0f, side * DroolSpreadUnits);   // the mouth joint's frame: x -> his forward (+Z), y -> down, z -> his sideways (+X)
                }
            _drool.Add(fx);
        }
    }
    private void StopDrool() { foreach (var fx in _drool) if (fx != null && IsInstanceValid(fx)) fx.Stop(); _drool.Clear(); }
    public int DroolStrands { get { int n = 0; foreach (var fx in _drool) if (fx != null && IsInstanceValid(fx) && fx.IsEmitting) n++; return n; } }
    /// <summary>Every live strand's ribbon faces the camera (DroolFaceCamera carried into SmsGoopStrand.FaceCamera); false with no strands.</summary>
    public bool DroolFacesCamera { get { bool any = false; foreach (var fx in _drool) { if (fx == null || !IsInstanceValid(fx)) continue; foreach (Node c in fx.GetChildren()) if (c is SmsGoopStrand s) { any = true; if (!s.FaceCamera) return false; } } return any; } }
    private void SetHidden(bool hidden)
    {
        _hidden = hidden;
        if (_model != null) _model.Visible = !hidden;
    }
    /// <summary>unk34C.a (K0_A) onto the materials' `opacity`: the screen mix. btelesa_down.brk (brk 1) lays its K0_A ramp over it: 0 -> 255 across its first 30
    /// frames (he blinks out and comes back solid on a spicy hit and on the down).</summary>
    private void ApplyAlpha(bool force = false)
    {
        float a = _alpha / 255f;
        if (_brk == 1) { float f = _brkTime / 2f; if (f < 30f) a = MathF.Min(a, f / 30f); }
        foreach (var m in _mats) m.SetShaderParameter("opacity", a);
    }
    /// <summary>unk348 (K1) onto the body's `tint` -- or the flash colour while a damage flash is ON (153g).</summary>
    private void ApplyTint()
    {
        Color c = FlashOn ? (DamageFlashFruitColor && _tint != Colors.White ? _tint : DamageFlashColor) : _tint;
        _bodyMat?.SetShaderParameter("tint", new Vector3(c.R, c.G, c.B));
    }
    /// <summary>K2_A onto the body's `mask_k`: the polmask threshold the brk sweeps -- 1 (the whole body in the tint) while a damage flash is ON (153g).</summary>
    private void ApplyMask() { _bodyMat?.SetShaderParameter("mask_k", FlashOn ? 1f : _maskK); }
    /// <summary>153g: the damage flash -- started by the hit (StartFlash), DamageFlashTicks long, DamageFlashPeriod ticks on then off; the tint and the mask
    /// re-applied on every edge (the readers MaskK / Tint keep the game's values: the flash is a layer over them).</summary>
    private int _flashLeft;
    public bool FlashOn => _flashLeft > 0 && DamageFlashPeriod > 0 && ((_flashLeft - 1) / DamageFlashPeriod & 1) == ((DamageFlashTicks - 1) / DamageFlashPeriod & 1);
    public int FlashTicksLeft => _flashLeft;
    private void StartFlash() { if (DamageFlashTicks <= 0) return; _flashLeft = DamageFlashTicks; ApplyTint(); ApplyMask(); }
    private void FlashTick()
    {
        if (_flashLeft <= 0) return;
        bool was = FlashOn;
        _flashLeft--;
        if (FlashOn != was) { ApplyTint(); ApplyMask(); }
    }
    /// <summary>setBrkFromIndex: the archive's three .brk (j3d_dump153.py): 0 btelesa_damage (K2_A 0 -> 150 over 20 frames: the body speckles into the fruit's tint),
    /// 1 btelesa_down (K0_A 0 -> 255 over 30 frames, held: see ApplyAlpha), 2 btelesa_lick (K2_A 150 through frame 39, then down to 0 at 99: the tint washes off).
    /// A frame = 2 ticks (the bck clock).</summary>
    private void SetBrk(int i) { _brk = i; _brkTime = 0; }
    private void BrkTick()
    {
        float f = _brkTime / 2f;
        float k = _maskK;
        if (_brk == 0) k = 150f / 255f * Mathf.Clamp(f / 20f, 0f, 1f);
        else if (_brk == 2) k = f <= 39f ? 150f / 255f : 150f / 255f * (1f - Mathf.Clamp((f - 39f) / 60f, 0f, 1f));
        else if (_brk == 1) k = 0f;
        _brkTime++;
        if (MathF.Abs(k - _maskK) > 1e-4f) { _maskK = k; ApplyMask(); }
    }
    /// <summary>setBtpFromIndex: 1 btelesa_tspicy (the rolled eyes, looped), 2 btelesa_twait (the 150-frame blink loop); a frame = 2 ticks.</summary>
    private void SetBtp(int i) { if (_btp != i) { _btp = i; _btpTime = 0; } }
    private static readonly int[] BlinkEnds = { 19, 21, 24, 26, 28, 30, 33, 35, 85, 87, 90, 92, 150 };   // the twait runs' ends (frames)
    private static readonly int[] BlinkEyes = { 0, 1, 2, 1, 0, 1, 2, 1, 0, 1, 2, 1, 0 };                   // open / half / close
    private void BtpTick()
    {
        int eye = 0;
        if (_btp == 1) eye = 3;
        else
        {
            int f = (_btpTime / 2) % 150;
            for (int i = 0; i < BlinkEnds.Length; i++) if (f < BlinkEnds[i]) { eye = BlinkEyes[i]; break; }
        }
        _btpTime++;
        ApplyEye(eye);
    }
    private void ApplyEye(int eye)
    {
        if (eye == _eyeNow || _bodyMat == null) return;
        Texture2D t = eye switch { 1 => EyeHalf, 2 => EyeClose, 3 => EyeRoll, _ => EyeOpen } ?? EyeOpen;
        if (t == null) return;
        _eyeNow = eye;
        _bodyMat.SetShaderParameter("base_tex", t);
    }
    /// <summary>mNormalAlpha (GhostOpacity x 255, read live: a knob set after the spawn still counts) + 30 x the hit points lost, clamped 0xFE: the ghost more
    /// solid each hit.</summary>
    private int TargetAlpha() { _normalAlpha = Math.Clamp(Mathf.RoundToInt(GhostOpacity * 255f), 1, 0xFE); return Math.Clamp(_normalAlpha + (Math.Max(1, HitPoints) - _hp) * 30, 0, 0xFE); }
    /// <summary>The spicy loop's bound emitters: the fire at joint 9 (jnt_sita_4) and the sweat at joint 1 (jnt_body), SmsNpcEffect scenes under BoneAttachment3Ds;
    /// the fire sound with them.</summary>
    private void StartSpicyFx()
    {
        if (_skel == null) return;
        if ((_spicyFire == null || !IsInstanceValid(_spicyFire)) && SpicyFireFxScene != null)
            _spicyFire = SmsNpcEffect.Spawn(SpicyFireFxScene, _skel, _bSpicy >= 0 ? "jnt_sita_4" : "jnt_sita_2", SpicyFireScale, _fxCache, Name);
        if ((_sweat == null || !IsInstanceValid(_sweat)) && SweatFxScene != null)
            _sweat = SmsNpcEffect.Spawn(SweatFxScene, _skel, "jnt_body", 3f, _fxCache, Name);
    }
    private void StopSpicyFx()
    {
        if (_spicyFire != null && IsInstanceValid(_spicyFire)) _spicyFire.Stop();
        if (_sweat != null && IsInstanceValid(_sweat)) _sweat.Stop();
        _spicyFire = null; _sweat = null;
    }
    private readonly Dictionary<string, BoneAttachment3D> _fxCache = new();
    /// <summary>A burst scene recoloured (SmsFxBurst.SetShaderParam on every JpaDirBillboard `prm` / `env`): the fruit-colour puffs (fhit_pe / _or / _gr, down_*).</summary>
    private void SpawnTintedBurst(PackedScene scene, Transform3D at, Color c)
    {
        if (scene == null) return;
        var fx = SpawnBurst(scene, at);
        if (fx is SmsFxBurst b)
        {
            b.SetShaderParam("prm", new Vector4(c.R, c.G, c.B, 1f));
            b.SetShaderParam("env", new Vector4(c.R * 0.45f, c.G * 0.45f, c.B * 0.45f, 1f));
        }
    }
    /// <summary>startAppearBalloon(id): the HUD's SmsHintBalloon (looked up once) with the knob's line.</summary>
    private void ShowHint(string text)
    {
        if (!ShowHints || string.IsNullOrEmpty(text)) return;
        if (!_hintLooked) { _hintLooked = true; _hint = FindHint(GetTree().Root); }
        if (_hint != null && IsInstanceValid(_hint)) _hint.Show(text, 1, true);
        else if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: hint (no SmsHintBalloon in the scene): {text}");
    }
    private static SmsHintBalloon FindHint(Node n)
    {
        if (n is SmsHintBalloon h) return h;
        foreach (Node c in n.GetChildren()) { var r = FindHint(c); if (r != null) return r; }
        return null;
    }
    /// <summary>SMSRumbleMgr->start(0x14, 0xf): a short strong buzz on the pad.</summary>
    private void RumblePad() { if (Rumble) Input.StartJoyVibration(0, 0.5f, 1f, 0.35f); }
    /// <summary>checkBckPass(f) for the current clip: it passed frame f this tick.</summary>
    private bool BckPassed(int clip, float f) => IsBck(clip) && _prevBck == clip && _prevBckFrame < f && BckFrame() >= f;

    /// <summary>onAllCollision / offAllCollision: his own filter and the body's and the tongue's.</summary>
    private void OnAllCollision() { _collisionOn = true; SetCollisionLayerValue(1, true); if (_bodyPart != null) _bodyPart.Enabled = true; if (_tonguePart != null) _tonguePart.Enabled = true; }
    private void OffAllCollision() { _collisionOn = false; SetCollisionLayerValue(1, false); if (_bodyPart != null) _bodyPart.Enabled = false; if (_tonguePart != null) _tonguePart.Enabled = false; }

    // ------------------------------------------------------------------------------------------------------------------ the tick
    /// <summary>TBossTelesa::moveObject's tail + calcRootMatrix's frame checks: the damage alpha, the spit clip's frame 40 (generateSlotItem in SpitSlotItem, else
    /// genAttacker), the spicy effects, the brk, the materials.</summary>
    protected override void SetBehavior()
    {
        if (_dead) return;
        if (IsNerve(NerveBossDie))                               // isInDamage: toward solid, or back down through the lick (past 50) and the laugh
        {
            int target = TargetAlpha();
            if ((IsBck(BckLick) && BckFrame() > 50f) || IsBck(BckLaugh)) { if (_alpha > target) _alpha--; }
            else if (_alpha < 0xFF) _alpha++;
        }
        else if (_opacitySeen != GhostOpacity) { _opacitySeen = GhostOpacity; _alpha = TargetAlpha(); }   // 153c: the slider is live -- every other nerve holds TargetAlpha() anyway
        if (BckPassed(BckSpit, 40f))
        {
            if (IsNerve(NerveSpitSlotItem)) GenerateSlotItem(); else GenAttacker();
        }
        if (IsBck(BckBeroHit) && BckFrame() < 20f && (_tickCount & 7) == 0 && SpicyHitFxScene != null)
            SpawnBurst(SpicyHitFxScene, new Transform3D(Basis.FromScale(Vector3.One * 3.5f), MouthUnits / U));
        if (IsBck(BckSpicy))                                     // calcRootMatrix: ASE at joint 1, SPICY_A..D at joint 9 every frame of clip 12, the FIRE sound
        {
            if (!_hidden) StartSpicyFx();
            if (FireSound != null && FireSound.Stream != null && !FireSound.Playing) FireSound.Play();
        }
        else if (_spicyFire != null || _sweat != null) StopSpicyFx();
        if (IsBck(BckLaugh) && BckPassed(BckLaugh, 2f) && LaughSound != null && LaughSound.Stream != null) LaughSound.Play();
        SetBtp(IsBck(BckSpicy) ? 1 : 2);
        BrkTick(); BtpTick(); FlashTick();
        ApplyAlpha();
        _prevBck = _bck; _prevBckFrame = BckFrame();
    }

    // ------------------------------------------------------------------------------------------------------------------ hits
    /// <summary>gpObjHitCheck's lists (every fourth tick): his own cylinder and the body's / the tongue's on Mario (ATTACK); the hose at close range on the parts;
    /// the fruit (MAP_OBJECTs) in the body / the tongue; the kill cylinder on the small enemies and Mario's distance to it.</summary>
    protected override void HitCheckMario()
    {
        if (_mario == null || _dead) return;
        Vector3 m = MarioPos();
        bool onGround = _mario.IsOnFloor();
        bool rising = !onGround && _mario.Velocity.Y > 0f;
        float ar = onGround ? SmsParams.Mario.HoldRadius : (rising ? SmsParams.Mario.PushupRadius : SmsParams.Mario.TrampleRadius);
        float ah = onGround ? SmsParams.Mario.AttackHeight : (rising ? SmsParams.Mario.PushupHeight : SmsParams.Mario.AttackHeight);
        Vector3 body = BodyPointUnits, tongue = TonguePointUnits;
        if (GetCollisionLayerValue(1) && !_hidden && CheckDistance(Pos(), P.AttackRadius, P.AttackHeight, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight)) AttackMario(Pos());
        if (_collisionOn && !_hidden)
        {
            if (CheckDistance(body, BodyHitRadius, BodyHitHeight, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight)) AttackMario(body);
            else if (CheckDistance(tongue, TongueHitRadius, TongueHitHeight, m, SmsParams.Mario.DamageRadius, SmsParams.Mario.DamageHeight)) AttackMario(tongue);
            if (_mario.SmsHoseSpraying)
            {
                if (CheckDistance(m, ar, ah, tongue, TongueHitRadius, TongueHitHeight)) SmsPartSprayed(SmsBossTelesaPart.Tongue, (m + Vector3.Up * 80f) / U);
                else if (CheckDistance(m, ar, ah, body, BodyHitRadius, BodyHitHeight)) SmsPartSprayed(SmsBossTelesaPart.Body, (m + Vector3.Up * 80f) / U);
            }
            CheckFruitHits(body, tongue);
        }
        KillSmallEnemies(m);
    }
    /// <summary>SMS_SendMessageToMario(ATTACK) from his cylinders.</summary>
    private void AttackMario(Vector3 fromUnits) => _mario?.TakeDamage(fromUnits / U, DamageProfile);
    /// <summary>TBossTelesaBody / Tongue::checkHit's MAP_OBJECT side: every fruit (the `pickable` group and his own spat ones) not in Mario's hands -- in the body
    /// cylinder -> checkHitObject; in the tongue cylinder: a RED_PEPPER -> setSpicy, else over tongue.y + 100 -> checkHitObject.</summary>
    private void CheckFruitHits(Vector3 body, Vector3 tongue)
    {
        var seen = new HashSet<Fruit>();
        foreach (Node n in GetTree().GetNodesInGroup("pickable")) if (n is Fruit f) seen.Add(f);
        foreach (Node3D n in _spit) if (n is Fruit f && IsInstanceValid(f)) seen.Add(f);
        foreach (Fruit f in seen)
        {
            if (!IsInstanceValid(f) || !f.IsInsideTree() || f.IsQueuedForDeletion()) continue;
            if (_spitFruitNoCollision.Contains(f)) continue;    // HIT_FILTER_NO_COLLISION until fruitCollisionOn
            if (f.SmsBallHeld || (_mario != null && _mario.SmsHoldsOrPends(f))) continue;
            Vector3 p = f.GlobalPosition * U;
            float r = MathF.Max(20f, f.SmsBallGeomRadius * U);
            bool inTongue = CheckDistance(p, r, r, tongue, TongueHitRadius, TongueHitHeight);
            bool inBody = CheckDistance(p, r, r, body, BodyHitRadius, BodyHitHeight);
            // the game runs the body's list then the tongue's on the same tick: a pepper in both is eaten by the body AND makes him spicy -- the tongue first here
            if (inTongue && f.Type == Fruit.FruitType.RedPepper) { SetSpicy(f); continue; }
            if (inBody) { CheckHitObject(f); continue; }
            if (inTongue && tongue.Y + TongueObjectOver < p.Y) CheckHitObject(f);
        }
    }
    /// <summary>TBossTelesaKillSmallEnemy::checkHit: every HIT_CATEGORY_ENEMY body in the 400 x 300 cylinder at the table's centre is killed (an unsprayed Boo
    /// attacker has the category off); Mario within 300 u of its point -> forceHide and the flag that holds HideWait.</summary>
    private void KillSmallEnemies(Vector3 m)
    {
        Vector3 k = KillPointUnits;
        foreach (Node n in GetTree().GetNodesInGroup("sms_enemy"))
        {
            if (n is not SmsSmallEnemy e || e == this || e.IsDead || !IsInstanceValid(e)) continue;
            if (e is SmsBossTelesa) continue;
            if (e is SmsTelesa t && !t.Tangible) continue;
            if (CheckDistance(k, KillRadius, KillHeight, e.GlobalPosition * U, e.TakeHitRadiusUnits, e.TakeHitHeightUnits)) e.Kill();
        }
        Vector3 d = m - k; d.Y = 0f;
        bool under = d.Length() < HideMarioRadius;
        _marioUnder = under;
        if (under && HidesWhenMarioUnder) ForceHide();
    }
    /// <summary>TBossTelesa::checkHitObject (PrepareSlot only): the fruit's kind -> his tint and kill (the flinch, or the spicy hit), a pepper just eaten; the
    /// splat at the fruit; the fruit dies.</summary>
    private void CheckHitObject(Fruit f)
    {
        if (!IsNerve(NervePrepareSlot)) return;
        int fx = -1;
        switch (f.Type)
        {
            case Fruit.FruitType.Coconut: _tint = Color.Color8(0xE6, 0x64, 0xB4); fx = 1; Kill(); break;
            case Fruit.FruitType.Papaya: case Fruit.FruitType.Pineapple: case Fruit.FruitType.Banana: _tint = Color.Color8(0xE6, 0xB4, 0); fx = 2; Kill(); break;
            case Fruit.FruitType.Durian: _tint = Color.Color8(0x96, 0x32, 0xE6); fx = 3; Kill(); break;
            case Fruit.FruitType.RedPepper: break;
            default: return;
        }
        _fruitHitFx = fx;
        if (fx >= 0) ApplyTint();
        if (!_spicy && f.Type != Fruit.FruitType.RedPepper)
        {
            if (_hintFirstFruit) { _hintFirstFruit = false; ShowHint(HintFirstFruit); }   // 0xE000F once
            _fruitHitCount++;
            if (_fruitHitCount > 2) ShowHint(HintFruitAgain);                            // 0xE0010
        }
        else _fruitHitCount = 0;
        Vector3 at = f.GlobalPosition;
        if (FruitHitFxScene != null) SpawnBurst(FruitHitFxScene, new Transform3D(Basis.FromScale(Vector3.One * 3.5f), at));   // scaled to him: the bursts are sized for a small enemy
        if (fx >= 0) SpawnTintedBurst(FruitPuffFxScene, new Transform3D(Basis.FromScale(Vector3.One * 2.5f), at), _tint);   // fhit_pe / _or / _gr
        if (_spicy && fx >= 0) { if (DamageFxScene != null) SpawnBurst(DamageFxScene, new Transform3D(Basis.FromScale(Vector3.One * 3.5f), at)); }
        else if (FruitHitSound != null && FruitHitSound.Stream != null) FruitHitSound.Play();
        if (_hintFirstHit) { _hintFirstHit = false; ShowHint(HintFirstHit); }           // 0xE0011 once
        RemoveSpit(f); ReleaseIfHeld(f);
        f.QueueFree();                                           // makeObjDead
        LastFruitType = f.Type;
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: {f.Type} on {(fx >= 0 ? "the body" : "him")} spicy {_spicy} -> hp {_hp} (the fruit at {f.GlobalPosition * U - BodyPointUnits} from the body point, vel {f.LinearVelocity * U / SmsUnits.TicksPerSecond} u/tick, frozen {f.Freeze}, held {f.SmsBallHeld})");
    }
    /// <summary>TBossTelesa::setSpicy: a pepper on the tongue (not while he spits the round, not during bero_hit) -> spicy, the bero_hit clip, the pepper eaten.</summary>
    private void SetSpicy(Fruit pepper)
    {
        if (IsNerve(NerveSpitSlotItem) || IsBck(BckBeroHit)) return;
        _spicy = true; _spicyTimer = 0;
        SetBck(BckBeroHit);
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: SPICY (the pepper on the tongue: {pepper.Name} at {pepper.GlobalPosition * U} vel {pepper.LinearVelocity * U / SmsUnits.TicksPerSecond} u/tick, frozen {pepper.Freeze}, held {pepper.SmsBallHeld}; the tongue point {TonguePointUnits}, my point {Pos()}, model y {JointUnits(0).Y:F0})");
        RemoveSpit(pepper); ReleaseIfHeld(pepper);
        pepper.QueueFree();
    }
    /// <summary>The hose on a part (the drops' rays through <see cref="SmsBossTelesaPart"/>, or the hose at close range): the body in PrepareSlot -> Freeze
    /// (checkMessage); the tongue in Appear -> SlotStart (tongueHitWater). The mark either way.</summary>
    public bool SmsPartSprayed(int part, Vector3 hitPos)
    {
        if (_dead || _hidden || !_collisionOn) return false;
        SmsWaterHitMarks.Emit(this, hitPos);
        if (part == SmsBossTelesaPart.Tongue) { if (IsNerve(NerveAppear)) { PushNerve(NerveSlotStart); if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: the tongue sprayed -> SlotStart"); } }
        else if (IsNerve(NervePrepareSlot)) PushNerve(NerveBossFreeze);
        return true;
    }
    /// <summary>His own body is not a spray target (receiveMessage: FALSE) -- the parts are.</summary>
    public override bool SmsSprayedByWater(Vector3 hitPos) => false;
    /// <summary>TBossTelesa::kill: the Die nerve pushed over the current one.</summary>
    public override void Kill()
    {
        if (_dead || IsNerve(NerveBossDie)) return;
        PushNerve(NerveBossDie);
    }
    /// <summary>forceHide: not while dying, hiding or appearing; the round swallowed, the sound, the spine reset onto Hide.</summary>
    public void ForceHide()
    {
        if (_dead || IsNerve(NerveBossDie) || IsBck(BckHide) || IsBck(BckAppear) || IsNerve(NerveHideWait)) return;
        ForceAllItemKill();
        _prepTimer = 0;
        PlayHideSound();
        _stack.Clear();
        SetNext(NerveHide);
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: forceHide (Mario under him)");
    }
    private void PlayHideSound() { var s = _spicy ? EscapeSound : DisappearSound; if (s != null && s.Stream != null) s.Play(); }

    // ------------------------------------------------------------------------------------------------------------------ the slot and the discs
    /// <summary>slotStart (the roll clip's frame 53): the drums spin and hold, every Boo attacker killed (telesaForceKill).</summary>
    private void SlotStartGo()
    {
        _slotRolling = true;
        Slot?.MoveStart();
        bool any = false;
        foreach (var a in _attackers) if (a != null && IsInstanceValid(a) && !a.IsDead) { a.Kill(); any = true; }
        if (any && DisappearSound != null && DisappearSound.Stream != null) DisappearSound.Play();
    }
    /// <summary>checkSlot: no drum held any more and a roll was started -> SpitSlotItem after this.</summary>
    private bool CheckSlot()
    {
        if (Slot == null) { if (_slotRolling) { _slotRolling = false; PushAfterCurrent(NerveSpitSlotItem); return true; } return false; }
        if (!Slot.IsRollDrum() && _slotRolling) { _slotRolling = false; PushAfterCurrent(NerveSpitSlotItem); return true; }
        return false;
    }
    /// <summary>rouletteStart: a random sense; the top and the bottom disc one way, the middle the other; each 0.05..0.1 + 0.03 x the hit points lost (x the
    /// knob), degrees a tick; the shake.</summary>
    public void RouletteStart()
    {
        float dir = Lerp(-1f, 1f, Rand());
        float hpSpeed = (Math.Max(1, HitPoints) - _hp) * RouletteUpRate;
        for (int i = 0; i < 3; i++)
        {
            float direction = dir > 0f ? (i == 1 ? -1f : 1f) : (i == 1 ? 1f : -1f);
            float speed = Lerp(0.05f, 0.1f, Rand());
            _rouletteSp[i] = direction * (hpSpeed + speed) * RouletteSpeedScale;
        }
        RollRouletteCircle();
        RumblePad();
        SmsCameraShake.Current?.StartMode(ShakeRoulette, 1f);
    }
    /// <summary>slotStop: every disc still.</summary>
    public void SlotStop() { for (int i = 0; i < 3; i++) _rouletteSp[i] = 0f; RollRouletteCircle(); }
    private void RollRouletteCircle() { for (int i = 0; i < 3; i++) if (_roulettes[i] != null && IsInstanceValid(_roulettes[i])) _roulettes[i].SetRollSp(_rouletteSp[i]); }
    /// <summary>TBossTelesa::fanfale (the slot's last drum stopped): confetti and the fanfare for FRUIT / COIN, the other one else.</summary>
    public void Fanfale()
    {
        int r = Slot != null ? Slot.SlotResult() : _slotResult;
        if (r == SmsTelesaSlot.Fruit || r == SmsTelesaSlot.Coin)
        {
            if (FanfareFxScene != null) SpawnBurst(FanfareFxScene, new Transform3D(Basis.Identity, (Slot != null ? Slot.GlobalPosition + Vector3.Up * (650f / U) : GlobalPosition)));
            var s = r == SmsTelesaSlot.Fruit ? Fanfare1Sound : Fanfare2Sound;
            if (s != null && s.Stream != null) s.Play();
        }
        else if (Fanfare3Sound != null && Fanfare3Sound.Stream != null) Fanfare3Sound.Play();
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: fanfale, the slot says {r}");
    }

    // ------------------------------------------------------------------------------------------------------------------ what he spits
    /// <summary>generateSlotItem (the spit clip's frame 40 in SpitSlotItem): by the slot's result -- FRUIT: 16 fruit and two peppers in a scattered 160-deg fan;
    /// COIN: ten coins; ENEMY / JACKPOT / Mixed: small enemies (none for the unported Bubbles).</summary>
    private void GenerateSlotItem()
    {
        _prepTimer = 0;
        _spit.Clear(); _spitFruitNoCollision.Clear();
        _slotResult = Slot != null ? Slot.SlotResult() : SmsTelesaSlot.Fruit;
        if (ForceResult != SlotForce.TheGame) _slotResult = ForcedResultValue();
        Vector3 mouth = MouthUnits;
        int slotItemNum = Prm.SlotItemNum;
        float angleStep = 120f / slotItemNum, angleOffset = angleStep * slotItemNum * 0.5f;
        if (_slotResult == SmsTelesaSlot.Fruit)
        {
            int numFruit = Math.Min(Prm.SlotFruitNum, 20);
            int angleIndex = _rng.Next(0, numFruit);
            float fruitAngleStep = 160f / numFruit;
            for (int i = 0; i < numFruit; i++)
            {
                Vector3 dir = PolarXZ(_yaw - angleOffset + fruitAngleStep * angleIndex, 200f);
                angleIndex = numFruit > 0 ? ((angleIndex + i) % numFruit + numFruit) % numFruit : 0;
                Vector3 nd = dir.Normalized();
                PackedScene scene; bool pepper = i == 0 || i == 4;
                if (pepper) scene = RedPepperScene;
                else if (i < 6) scene = CoconutScene; else if (i < 12) scene = PapayaScene; else if (i < 14) scene = PineappleScene; else scene = DurianScene;
                if (scene == null) continue;
                float k = (pepper && i == 0 ? 2f : 1f) * SpitSpeedScale;
                Vector3 vel = new Vector3(nd.X * Lerp(6f, 10f, Rand()) * k, -2f, nd.Z * Lerp(6f, 10f, Rand()) * k);
                SpawnFruit(scene, new Vector3(mouth.X + dir.X, mouth.Y - 50f, mouth.Z + dir.Z), vel, pepper);
            }
        }
        else if (_slotResult == SmsTelesaSlot.Coin)
        {
            int numItems = Math.Min(Prm.SlotFruitNum, 10);
            float itemAngleStep = 120f / numItems, itemAngleOffset = itemAngleStep * numItems * 0.5f;
            if (_coinRoundsLeft > 0) _coinRoundsLeft--;
            for (int i = 0; i < numItems && i < 10; i++)
            {
                Vector3 dir = PolarXZ(_yaw - itemAngleOffset + itemAngleStep * i, 250f).Normalized();
                dir.Y = 10f;
                float sp = Prm.FirstBubbleSp * SpitSpeedScale;
                Vector3 vel = new Vector3(dir.X * sp * Lerp(0.8f, 3.5f, Rand()), dir.Y, dir.Z * sp * Lerp(0.8f, 3.5f, Rand()));
                SpawnCoin(new Vector3(mouth.X, mouth.Y - 250f, mouth.Z), vel);
            }
        }
        else
        {
            int count = slotItemNum, kind = 0;
            if (_slotResult == SmsTelesaSlot.Mixed) count *= 2;
            else if (_slotResult == SmsTelesaSlot.Enemy) kind = _hp > 2 ? 1 : 2;
            int maxKind = _hp == 1 ? 8 : 7;
            int randomKind = _rng.Next(1, maxKind + 1);
            for (int i = 0; i < count; i++)
            {
                if (_slotResult == SmsTelesaSlot.Jackpot)
                {
                    if (i % 2 == 0) randomKind++;
                    if (randomKind > maxKind) randomKind = 1;
                    kind = randomKind;
                }
                PackedScene scene = kind < SlotEnemyScenes.Count ? SlotEnemyScenes[kind] : null;
                if (scene == null) continue;
                Vector3 dir = PolarXZ(_yaw - angleOffset + angleStep * i, 200f).Normalized();
                dir.Y = 2f;
                float sp = Prm.FirstBubbleSp * SpitSpeedScale;
                Vector3 vel = new Vector3(dir.X * sp * Lerp(0.5f, 1f, Rand()), dir.Y * (2f + Lerp(0.5f, 1f, Rand())), dir.Z * sp * Lerp(0.5f, 1f, Rand()));
                SpawnEnemy(scene, new Vector3(mouth.X, mouth.Y - 250f + 10f, mouth.Z), vel, kind != 0);
            }
        }
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: generateSlotItem result {_slotResult}: {_spit.Count} things");
    }
    /// <summary>A fruit of the pool at the mouth with the game's velocity (units a tick -> m/s for the fruit's ball sim), scale 1.5, out of Mario's reach until
    /// fruitCollisionOn (off the `pickable` group; no hits).</summary>
    private Fruit SpawnFruit(PackedScene scene, Vector3 atUnits, Vector3 velUnits, bool pepper)
    {
        var f = scene.Instantiate<Fruit>();
        f.Size = 1.5f;
        GetParent().AddChild(f);
        f.GlobalPosition = atUnits / U;
        if (pepper) f.Rotation = new Vector3(0f, Mathf.DegToRad(90f), 0f);
        f.LinearVelocity = velUnits * (SmsUnits.TicksPerSecond / U);
        if (f.IsInGroup("pickable")) f.RemoveFromGroup("pickable");
        _spitFruitNoCollision.Add(f);
        _spit.Add(f);
        return f;
    }
    private void SpawnCoin(Vector3 atUnits, Vector3 velUnits)
    {
        if (CoinScene == null) return;
        var coin = CoinScene.Instantiate<Node3D>();
        GetParent().AddChild(coin);
        coin.GlobalPosition = atUnits / U;
        SmsDropTable.Launch(coin, velUnits, 960, 0f);           // killByTimer(0x3C0)
        _spit.Add(coin);
    }
    private void SpawnEnemy(PackedScene scene, Vector3 atUnits, Vector3 velUnits, bool track)
    {
        var node = scene.Instantiate<Node3D>();
        if (node is not SmsSmallEnemy e) { node.QueueFree(); return; }
        e.RelaunchTicks = -1;                                    // the round's enemies die for good
        GetParent().AddChild(e);
        e.SmsLaunch(atUnits / U, _yaw + Lerp(-60f, 60f, Rand()), velUnits, null, true);
        if (track) _spit.Add(e);
    }
    /// <summary>fruitCollisionOn (PrepareSlot's start): the spat fruit can be taken and can hit him.</summary>
    private void FruitCollisionOn()
    {
        foreach (Fruit f in _spitFruitNoCollision) if (IsInstanceValid(f) && f.IsInsideTree() && f.CanBePickedUp && !f.IsInGroup("pickable")) f.AddToGroup("pickable");
        _spitFruitNoCollision.Clear();
    }
    /// <summary>checkAllItemDead: a Mixed round at once; else every spat thing gone (eaten, collected, killed).</summary>
    private bool CheckAllItemDead()
    {
        if (_slotResult == SmsTelesaSlot.Mixed) return true;
        foreach (Node3D n in _spit)
        {
            if (n == null || !IsInstanceValid(n) || !n.IsInsideTree() || n.IsQueuedForDeletion()) continue;
            if (n is SmsSmallEnemy e && e.IsDead) continue;
            return false;
        }
        return true;
    }
    /// <summary>forceAllItemKill: everything he spat swallowed back with a puff (a fruit in Mario's hands too: his hands empty, the game's DETACH).</summary>
    private void ForceAllItemKill()
    {
        foreach (Node3D n in _spit) KillSpit(n);
        _spit.Clear(); _spitFruitNoCollision.Clear();
    }
    /// <summary>damageRecover (the spicy hit): the fruit, the peppers and the coins swallowed -- the round's enemies stay -- then Hide.</summary>
    private void DamageRecover()
    {
        for (int i = _spit.Count - 1; i >= 0; i--)
        {
            Node3D n = _spit[i];
            if (n is SmsSmallEnemy) continue;
            KillSpit(n);
            _spit.RemoveAt(i);
        }
        _spitFruitNoCollision.Clear();
        PlayHideSound();
        PushAfterCurrent(NerveHide);
        _prepTimer = 0;
    }
    private void KillSpit(Node3D n)
    {
        if (n == null || !IsInstanceValid(n) || n.IsQueuedForDeletion()) return;
        if (n is SmsSmallEnemy e) { if (!e.IsDead) { if (ItemKillFxScene != null && e.IsInsideTree()) SpawnBurst(ItemKillFxScene, new Transform3D(Basis.Identity, e.GlobalPosition)); e.SmsKillByDemo(); } return; }
        ReleaseIfHeld(n);
        if (n.IsInsideTree() && ItemKillFxScene != null) SpawnBurst(ItemKillFxScene, new Transform3D(Basis.Identity, n.GlobalPosition));
        n.QueueFree();
    }
    /// <summary>153f: a spit thing about to die while Mario HOLDS it (a fruit in his hands when the fan is swallowed -- the hide, a hit, the down, his walk under
    /// him) empties his hands first (Mario.SmsHeldGone: pass 130b's ensureTakeSituation, the Bob-omb that blew up in them). Without it he kept carrying a
    /// freed body -- stuck in the carry until something hit him (Jaysonn's softlock).</summary>
    private void ReleaseIfHeld(Node3D n)
    {
        if (n is not RigidBody3D rb || _mario == null || !IsInstanceValid(_mario) || !_mario.SmsHoldsOrPends(rb)) return;
        _mario.SmsHeldGone(rb);
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: the {n.Name} in Mario's hands is gone -> his hands emptied");
    }
    private void RemoveSpit(Node3D n) { _spit.Remove(n); if (n is Fruit f) _spitFruitNoCollision.Remove(f); }
    /// <summary>flashItem(result): the last 120 ticks of a round the fruit (not in his hands), the peppers and the coins blink 8 ticks in 16.</summary>
    private void FlashItem(int result)
    {
        int phase = ((result % 16) + 16) % 16;
        bool hidden = phase < 8;
        foreach (Node3D n in _spit)
        {
            if (n == null || !IsInstanceValid(n) || !n.IsInsideTree() || n is SmsSmallEnemy) continue;
            if (n is Fruit f && (f.SmsBallHeld || (_mario != null && _mario.SmsHoldsOrPends(f)))) continue;
            n.Visible = !hidden;
        }
    }
    /// <summary>genAttacker: a Boo of the pool made an attacker at his mouth (TTelesa::initAttacker). The pool is Telesa.tscn bodies under his parent, dead
    /// until he needs them.</summary>
    public void GenAttacker()
    {
        if (!SpitsBooAttackers || BooScene == null) return;
        SmsTelesa boo = null;
        foreach (var a in _attackers) if (a != null && IsInstanceValid(a) && a.IsDead) { boo = a; break; }
        if (boo == null)
        {
            if (_attackers.Count >= MaxAttackers) return;        // makeOneEnemyAppear: none free
            boo = BooScene.Instantiate<SmsTelesa>();
            boo.Pooled = true;
            boo.Name = $"{Name}Boo{_attackers.Count}";
            GetParent().AddChild(boo);
            boo.SmsSpawnDead();
            _attackers.Add(boo);
        }
        boo.JumpPopsUnsprayed = AttackersJumpable;               // 153h
        boo.FirstPointTowardMario = AttackersFirstPointTowardMario; boo.SmoothAttack = AttackersSmooth;   // 153j
        // 153j: out of his MOUTH, not his skull -- jnt_mouth sits 220 u inside the head (the face is at z 318), and the game's 10 hidden ticks did not carry the
        // Boo out of it; AttackersSpawnForwardUnits along his facing puts it at the lips. Its floor is the TABLE'S SURFACE (the game's absolute 500: the casino
        // table), not his own floor -- 153c made that the pressed bowl's bottom, 143 u under the pads, and the Boos flew inside the rings' floor.
        boo.SmsInitAttacker(this, MouthUnits + PolarXZ(_yaw, AttackersSpawnForwardUnits), _yaw, TableSurfaceUnits, SpitSpeedScale);
        RumblePad();                                             // TNerveBossTelesaSpit's SMSRumbleMgr->start(0x14, 0xf)
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: a Boo attacker ({AttackersAlive} out)");
    }
    /// <summary>The Shine (makeShineAppearWithDemo "the boss shine"): the ShineScene at the ShineSpawnPoint, else his point, with its entrance.</summary>
    private void SpawnShine()
    {
        _shineDone = true;
        if (ShineScene == null) return;
        var shine = ShineScene.Instantiate<Node3D>();
        GetParent().AddChild(shine);
        shine.GlobalPosition = ShineSpawnPoint != null ? ShineSpawnPoint.GlobalPosition : GlobalPosition;
        if (shine is ShineSprite s) s.PlaySpawnEntrance();
        if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: the Shine at {shine.GlobalPosition}");
    }
    /// <summary>LIVE_FLAG_DEAD at the end: the slot and the parts gone with him, the attackers too (DEVIATION: the game's manager keeps its Boos).</summary>
    protected override void Dead()
    {
        foreach (var a in _attackers) if (a != null && IsInstanceValid(a) && !a.IsDead) a.SmsKillByDemo();
        if (Slot != null && IsInstanceValid(Slot)) Slot.Visible = false;
        OffAllCollision();
        StopSpicyFx(); StopDrool(); _droolOn = false;
        SetHidden(true);
        PlaceAmbientFx();
        base.Dead();
    }

    // ------------------------------------------------------------------------------------------------------------------ the nerves
    protected override bool ExecuteNerve(int n)
    {
        switch (n)
        {
            case NerveHideWait:                                  // TNerveBossTelesaHideWait
                if (_time == 0)
                {
                    SetHidden(true); _spicy = false; _alpha = TargetAlpha(); SetBrk(2); _maskK = 0f; ApplyMask(); StopSpicyFx();
                    // 153c: the slot machine stays shown -- it is sinking below the table (SlotSinkSpeed) and rises back with him at the appear
                }
                if (_intro != 0) return false;                   // the FallDemo runs first (IntroTick); its end restarts this clock
                if (_time > HideWaitTicks && !_marioUnder) { PushAfterCurrent(NerveAppear); SetHidden(false); return true; }
                return false;

            case NerveAppear:                                    // TNerveBossTelesaAppear
                if (_time == 0)
                {
                    if (!IsBck(BckAppear))
                    {
                        SetBck(BckAppear);
                        _alpha = TargetAlpha();                      // the knob read live (GhostOpacity set after the spawn)
                        if (!_bgmStarted) { _bgmStarted = true; if (BossMusic != null && BossMusic.Stream != null && !BossMusic.Playing) BossMusic.Play(); }   // unk384: the boss BGM
                        if (Slot != null) { Slot.Visible = true; Slot.RandomReset(); }
                        OnAllCollision();
                    }
                }
                else if (BckEnded() && !IsBck(BckWait)) SetBck(BckWait);
                if (BckPassed(BckAppear, 78f)) { SmsCameraShake.Current?.StartMode(ShakeLand, 1f); if (LandSound != null && LandSound.Stream != null) LandSound.Play(); }
                if (_time > AppearSpitAfterTicks && SpitsBooAttackers)
                {
                    int interval = TelesaGenerateInterval + (Math.Max(1, HitPoints) - _hp) * 100;
                    if (_time % interval == 1) SetBck(BckSpit);
                }
                _hoseiY *= 0.96f;
                return false;

            case NerveSlotStart:                                 // TNerveBossTelesaSlotStart
                if (_time == 0) SetBck(BckRoll);
                if (IsBck(BckRoll))
                {
                    if (BckPassed(BckRoll, 53f)) SlotStartGo();
                    if (BckEnded())
                    {
                        SetBck(BckWait);
                        if (Slot != null) { Slot.ForcedResult = ForcedResultValue(); Slot.ForceStopSlot(1, FirstHitCollectRate, _mario != null ? _mario.SmsHP : 8, _slotResult, _coinRoundsLeft, _rng); }
                    }
                }
                if (CheckSlot()) return true;
                _hoseiY *= 0.99f;
                return false;

            case NerveSpitSlotItem:                              // TNerveBossTelesaSpitSlotItem
                if (_time == 0) _pauseTicks = 0;
                if (Slot != null && Slot.IsRolling && !IsBck(BckSpit)) { _pauseTicks = 0; return false; }   // 153b: the last drum stops first (the game: 200 from the last release)
                if (!IsBck(BckSpit) && _hoseiY < BaseHoseiPosY - 200f && SlotSunk) SetBck(BckSpit);   // 153g: and the machine all the way under the table first (Jaysonn; the game's vanished in a frame)
                else if (IsBck(BckSpit) && BckEnded() && _time > 600) { PushAfterCurrent(NervePrepareSlot); _prepTimer = 0; FruitCollisionOn(); return true; }
                else if (_pauseTicks++ > ResultPauseTicks) _hoseiY -= 100f;   // the pause over: the dip (the machine drops out of sight with it) and the spit
                return false;

            case NervePrepareSlot:                               // TNerveBossTelesaPrepareSlot
            {
                if (_time == 0) SetBck(BckWait);
                if (_spicy)
                {
                    _spicyTimer++;
                    if (BckEnded())
                    {
                        if (IsBck(BckBeroHit)) SetBck(BckSpicy);
                        else if (IsBck(BckSpicy)) { if (_spicyTimer > Prm.SpicyTime) SetBck(BckSpicyEnd); }
                        else { _spicyTimer = 0; _spicy = false; _alpha = TargetAlpha(); SetBck(BckWait); }
                    }
                }
                _prepTimer++;
                int waitTime = _hp == 2 ? Prm.StopSlotTime1 : (_hp == 1 ? Prm.StopSlotTime2 : Prm.StopSlotTime0);
                if (_slotResult == SmsTelesaSlot.Coin) waitTime = (int)(waitTime * 0.5f);
                waitTime = (int)(waitTime * RoundTimeScale);
                if (_prepTimer > waitTime - 120) FlashItem(waitTime - _prepTimer);
                if (IsBck(BckWait) && (CheckAllItemDead() || _prepTimer > waitTime))
                {
                    _prepTimer = 0;
                    ForceAllItemKill();
                    PlayHideSound();
                    _stack.Clear();
                    PushAfterCurrent(NerveHide);
                    return true;
                }
                return false;
            }

            case NerveHide:                                      // TNerveBossTelesaHide
                if (!IsBck(BckHide)) SetBck(BckHide);
                if (BckEnded()) { OffAllCollision(); RouletteStart(); PushAfterCurrent(NerveHideWait); return true; }
                return false;

            case NerveSpit:                                      // TNerveBossTelesaSpit (an attacker on demand: the frame-40 check spawns it)
                if (_time == 0 || !IsBck(BckSpit)) SetBck(BckSpit);
                return _time > 0 && BckEnded();

            case NerveBossFreeze:                                // TNerveBossTelesaFreeze (the body hosed while he waits on the table)
                if (IsBck(BckWet)) { if (BckEnded()) { _spicy = false; _alpha = TargetAlpha(); return true; } }
                else { SetBck(BckWet); if (ThankYouSound != null && ThankYouSound.Stream != null) ThankYouSound.Play(); }
                return false;

            case NerveBossDie:                                   // TNerveBossTelesaDie
                if (_time == 0)
                {
                    _dieTimer = 0;
                    if (_spicy) _hp = Math.Max(0, _hp - 1);
                    OffAllCollision();
                    if (_hp != 0)
                    {
                        if (_spicy) { SetBrk(1); SetBck(BckDamage); SmsCameraShake.Current?.StartMode(ShakeDamage, 1f); }
                        else { SetBck(BckHit); SetBrk(0); SmsCameraShake.Current?.StartMode(ShakeHit, 1f); }
                        StartFlash();                                    // 153g: Jaysonn's damage flash (the flinch and the spicy hit alike)
                    }
                    else
                    {
                        SmsCameraShake.Current?.StartMode(ShakeDown, 1f);
                        SetBck(BckDown); SetBrk(1);
                        if (Slot != null) Slot.Visible = false;
                        if (DownSound != null && DownSound.Stream != null) DownSound.Play();
                        if (BossMusic != null && BossMusic.Playing) { var tw = CreateTween(); tw.TweenProperty(BossMusic, "volume_db", -60f, 10f / 30f); tw.TweenCallback(Callable.From(BossMusic.Stop)); }   // MSBgm::stopBGM(.., 10)
                    }
                    if (DebugPrint) GD.Print($"[SMS btelesa] {Name}: Die (spicy {_spicy}) -> hp {_hp}");
                }
                if (_hp == 0)
                {
                    if (BckEnded())
                    {
                        if (_dieTimer == 0)
                        {
                            Vector3 bodyAt = JointUnits(_bBody) / U;
                            if (DownFxScene != null) SpawnBurst(DownFxScene, new Transform3D(Basis.FromScale(Vector3.One * 5f), bodyAt));                  // ms_btls_down
                            if (_fruitHitFx >= 0) SpawnTintedBurst(DownFxScene, new Transform3D(Basis.FromScale(Vector3.One * 6f), bodyAt), _tint);   // ms_btls_down_pe / _gr / _or
                            ForceAllItemKill();
                            SlotStop();
                        }
                        if (_dieTimer > DownToShineTicks)
                        {
                            _dieTimer = 0;
                            SpawnShine();
                            OffAllCollision();
                            _stack.Clear();
                            Dead();
                            return true;
                        }
                        _dieTimer++;
                    }
                    return false;
                }
                if (BckEnded())
                {
                    if (IsBck(BckHit)) { SetBrk(2); SetBck(BckLick); }
                    else if (IsBck(BckLick)) SetBck(BckLaugh);
                    else
                    {
                        if (IsBck(BckLaugh))
                        {
                            OnAllCollision();
                            SetBck(BckWait);
                            _stack.Clear();
                            PushAfterCurrent(NervePrepareSlot);
                        }
                        else DamageRecover();
                        return true;
                    }
                }
                return false;
        }
        return base.ExecuteNerve(n);
    }
}

/// <summary>
/// One of King Boo's hit actors (TBossTelesaBody / TBossTelesaTongue) as a body the hose's drops can hit: a StaticBody3D cylinder on the hidden-coin layer
/// (the drops' rays include it; the fruit sim, Mario and the enemies ignore it) that follows its joint each frame and hands a spray to the boss with its part.
/// Mario and the fruit meet the same cylinders in the boss's own maths.
/// </summary>
public partial class SmsBossTelesaPart : StaticBody3D, ISmsSprayTarget
{
    public const int Body = 0, Tongue = 1;
    private const float U = SmsUnits.UnitsPerMetre;
    private SmsBossTelesa _owner; private int _part; private CollisionShape3D _cs; private CylinderShape3D _cyl;
    public void Setup(SmsBossTelesa owner, int part, float radiusUnits, float heightUnits)
    {
        _owner = owner; _part = part;
        TopLevel = true;
        CollisionLayer = SmsHiddenCoin.HideLayer; CollisionMask = 0;
        _cyl = new CylinderShape3D { Radius = radiusUnits / U, Height = heightUnits / U };
        _cs = new CollisionShape3D { Shape = _cyl, Name = "Cylinder", Position = new Vector3(0f, heightUnits * 0.5f / U, 0f) };
        AddChild(_cs);
    }
    public bool Enabled { get => _cs != null && !_cs.Disabled; set { if (_cs != null) _cs.Disabled = !value; } }
    /// <summary>The cylinder's base point (the game's hit actor position), metres.</summary>
    public void Place(Vector3 baseGlobal) { if (IsInsideTree()) GlobalPosition = baseGlobal; }
    public bool SmsSprayedByWater(Vector3 hitPos) => _owner != null && IsInstanceValid(_owner) && _owner.SmsPartSprayed(_part, hitPos);
}
