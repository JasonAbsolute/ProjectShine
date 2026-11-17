using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// SMS pass 157: KING BOO as the REMATCH fights him (KINGBOO_REMATCH_gdoc.txt) -- the arena's own actor, apart from the casino's
/// SmsBossTelesa (the roulette fight stays as it is). This pass is his ENTRANCE: hidden until <see cref="Spawn"/>, then the appear clip, the
/// laugh clip with the long laugh (MSD_SE_BS_TELESA_V_LAUGH1), and the wait loop -- hovering (HoverBob), turning to face Mario (FaceMario),
/// the soft glow and the twinkle at his body (the casino boss's GlowFx / ChikaFx, "a slight glow on him throughout"), the blink loop on his
/// eyes (btelesa_twait's 150-frame table), the ghost's screen mix at GhostOpacity. Pass 158: HIS HIT POINTS -- a thrown Bomb Boo bursting on him
/// (SmsBombBoo; 159: SWUNG into his body's hit cylinder, HitRadius / HitBelow / HitAbove) is <see cref="TakeHit"/>: a point off, the hit clip
/// with a full turn about himself (the doc's hurt flip, until its own animation comes), a flash, more solid (OpacityPerHit); at none left the
/// down clip and he is gone (Defeated).
/// Pass 162: THE TELEPORTS AND THE LICK (the doc: "quick and light... teleports between spots... does not walk"; "he teleports directly in front of
/// Mario and licks, with half a second of wind-up before it lands. The wind-up is the whole tell and a jump clears it"). While he waits a gap runs
/// down (GapAtStart at the first hit point to GapAtLastHit at the last, +- GapJitter: the doc's pace, shorter per hit); at its end a MOVE: he
/// VANISHES (the hide clip -- its hop up -- under a fade of TeleportOutSeconds, MSD_SE_BS_TELESA_DISAPPEAR), and ARRIVES somewhere else (a fade in
/// of TeleportInSeconds, settling down TeleportInDrop, the appear voice): usually LickDistance in front of Mario, facing him, at LickHeight over the
/// floor, for the LICK; by RepositionChance just another spot of the arena (SpotRadius about its middle, SpotMinFromMario to SpotMaxFromMario from
/// Mario) and a half gap. The lick is a casino clip (LickClip: btelesa_roll, the roulette spin, at LickClipSpeed): he rears up with the tongue
/// raised overhead (the tell, MSD_SE_BS_TELESA_V_ROLL), then plunges as the tongue WHIPS across the front of him, left to right, low -- the wet whip
/// (163: Jaysonn; 162 had re-aimed the tongue onto Mario, which looked wrong) -- with the tongue a bit longer than the clips have it
/// (<see cref="SmsTongueStretch"/>, TongueStretch). The HIT: over the whip's front crossing (clip frames LickHitFromFrame..LickHitToFrame,
/// MSD_SE_BS_TELESA_LICK at its start) any tongue joint, or the point between two, inside Mario's damage cylinder (42 u across, 130 u tall from his
/// feet) grown by LickThickness is the plain hit -- Mario.TakeDamage with the common enemy profile, the knockdown -- once per lick; the whip comes
/// through at Mario's legs from LickHeight (the tip skimming the floor), so a jump at the tell clears it. The clip is cut at LickEndFrame for the wait; AfterLickSeconds later he
/// vanishes to another spot and the gap runs again. He can be hit by a swung Bomb Boo while he waits AND through the lick (the hit breaks the lick
/// off: the hurt as before); not while he is between spots.
/// Pass 164: THE BOOLOSSUS SMASH (the doc's attack 2: "Crown glows white. A Boolossus falls from above Mario and tries to slam down on him. On landing it
/// splits into 15 Boos that spread out as a shockwave, then vanish after one second"). A move is now one of three (the doc's near-random pick):
/// a hop to another spot (RepositionChance), else an attack by weight (166; 164 had a SmashChance). The smash is made where he stands: the laugh clip
/// (SmashClip, the second laugh voice) with his CROWN SHINING the attack's colour (the doc: white for the smash; 165, Jaysonn: "king boo's crown should
/// glow the color listed in the doc to show that's the attack he's using ... a shiny glow, not a light-bulb glow") -- the crown shader's shine
/// (crown_glow / crown_glow_k: the sphere map's highlights sliding over it and a rim, in the colour), its colour register, SPARKLES at the crown (the
/// twinkle stars in the colour, CrownSparkles) and a faint halo (CrownGlowSize), all flashing up at the activation (CrownFlashSeconds) and then pulsing,
/// quicker and quicker, through SmashTellSeconds, the tell, then held -- then the Boolossi: 165, SmashCount of them (three), SmashStagger apart, each the Boolossus scene (SmashScene:
/// <see cref="SmsBoolossus"/>, its own knobs) DIVING in at Mario from its own direction (SmashSpreadDeg apart about a random first; from where King
/// Boo stands when SmashFromKingBoo) and running on its own (the dive, the slam, the wave of Boos); he watches SmashWatchSeconds past the last, the
/// crown dark again, and the wait and the gap run again. A swung Bomb Boo hits him through the tell (the smash is called off; Boolossi already out
/// carry on). The lick has no crown colour (the doc).
/// Pass 166: GHOST LIGHTNING (the doc's attack 3: "Crown glows red. Two walls of lightning close in on Mario from either side and try to squash him.
/// Three times in a row, fast"). The attacks are picked by WEIGHT now (LickWeight / SmashWeight / LightningWeight; the doc: near-random), a hop first by
/// RepositionChance. The lightning is made where he stands: the crown RED (LightningCrownColor) through LightningTellSeconds with the laugh, then an
/// <see cref="SmsGhostLightning"/> about Mario (its own knobs: the walls, the three squeezes, the shock) watched to its end, LightningWatchSeconds more,
/// the crown dark, the wait and the gap. A swung Bomb Boo hits him through it (the walls go). The faces and the other attacks are
/// later passes; the animations another hand's. The model is the casino boss's (btelesa.glb at 1 / 74.44) with its shader materials
/// (BossTelesaBody/Crown/Ha/Mouth.tres: opacity, tint, mask_k, base_tex), copied per instance. Signals: Spawned (the appear starts), Laughed
/// (the laugh starts), ReadyForFight (the wait begins -- the fight may start), Hit, Defeated; 162: Teleported (he has arrived somewhere), LickStarted
/// (the wind-up), Licked(hit) (the whip has passed, with whether it got him); 164: SmashStarted (the tell), Smashed(hit) (the Boolossus has landed); 166:
/// LightningStarted (the tell), LightningSqueezed(hit) per squeeze.
/// Pass 167: GHOST BREATH (the doc's attack 4: "Crown glows frosty blue. He blows cold breath in a sweep across the arena, left to right or right to left. A
/// hit freezes Mario for one second"; "the one reaction Sunshine does not have ... the one second freeze is a new state"). Made where he stands: the crown
/// FROSTY BLUE (BreathCrownColor) through BreathTellSeconds as the casino spit clip rears his head back, the body turning from Mario toward the side the
/// sweep starts from (the tell shows the way), then the thrust -- the clip held at it -- and the COLD out of his mouth (<see cref="SmsGhostBreath"/>, its own
/// knobs: the stream of mist that rolls along the floor, the sweep, the hit) sweeping through SweepDeg over SweepSeconds with his body turning with it,
/// from Mario's left or right (SweepWay). The hit is the FREEZE: Mario.SmsFreezeFor(FreezeSeconds) -- the kick freeze's skip of his move held for the
/// doc's second with his pose held and the frost fog on him, no HP -- and the ICE about him (<see cref="SmsIceBlock"/>) that shatters as he thaws; the
/// cold hugs the floor, so a jump as it reaches him clears it. BreathWatchSeconds after the sweep, the wait and the gap (the cold still rolling). A swung
/// Bomb Boo hits him through it (the breath cuts off). Signals BreathStarted (the tell), Breathed(hit) (the sweep is over).
/// Pass 168: FIREBALLS (the doc's attack 5: "Crown glows red. Fireballs shoot off in random directions and bounce off the arena walls. They shrink as
/// they travel and fade out"; the hit "the burn ... fireDown on the profile": Mario.SmsDamageProfile.Fire). Made where he stands: the crown RED-ORANGE
/// (FireballCrownColor; the lightning's red is deeper -- the doc has both red) through FireballTellSeconds as the spit clip rears, then the thrust (the
/// clip held at it) and -- 172, Jaysonn: "more like molten lava balls ... from the crown popping a few out at once" -- POPS of LAVA BALLS out of the CROWN
/// (<see cref="SmsFireballs"/>, its own knobs: the pops and how many each, the lob, where they come down about Mario, the size, the life, the roll, the
/// bounces, the burn), FireballPopSeconds apart, facing Mario throughout; FireballWatchSeconds after the last pop, the wait (the balls rolling on their
/// own). A swung Bomb Boo hits him through it (the balls fizzle out). Signals FireballsStarted (the tell), FireballsShot (the last pop is out),
/// FireballBurned (a ball got Mario), FireballsDone(burns) (the last ball has died).
/// Pass 169: GHOST WIND (the doc's attack 6: "Crown glows pink, for now. Tornados come at Mario and fling him around the arena"; the hit "knocked back and
/// flung": the port's fling since pass 94, Mario.SmsThrowMario -- the Cataquack's). Made where he stands: the crown PINK (WindCrownColor) through
/// WindTellSeconds with the laugh, then <see cref="SmsGhostWind"/> (its own knobs: the tornados' count, where they rise, their speed and turning, their
/// life; the funnel; the fling) -- TornadoCount tornados rising out of the floor away from Mario and coming at him for TornadoSeconds -- watched
/// WindWatchSeconds, then the wait (the tornados hunting on their own). A swung Bomb Boo hits him through it (the tornados dissipate). Signals WindStarted
/// (the tell), WindSpawned(count), Flung (a tornado got Mario), WindDone(flings).
/// Pass 177 (Jaysonn: "make king boo do his flip when hurt like in the normal fight" / "every 3 hits king boo will make a copy of himself like in luigis
/// mansion 3 ... mario has to hit the real one for it to do any damage. this king boo however tends to stay away and more known to throw attacks not as fast
/// or commonly"): the HURT is the casino boss's tumble (HurtClip btelesa_damage at HurtClipSpeed: head over heels, down and back up) -- and as he recovers
/// from every CopyEveryHits-th hit a COPY splits off (a flash; a duplicate of this node, IsCopy, fading in at a far spot, laughing; MaxCopies out at once).
/// A copy looks and moves like him, keeps CopyMinFromMario from Mario, waits CopyGapScale longer and only THROWS (the lava balls / the breath, at
/// CopySpeedScale). HitPoints 9. His defeat takes his copies with him (DismissCopy: a laugh, a fade). Every King Boo is in the group sms_rematch_boo (the
/// Bomb Boos' targets); only the real one in sms_rematch_boss. Signals CopyMade(copy), CopyHit(copy); readouts IsCopy, Original, CopiesAlive, CopiesMade, Copies.
/// Pass 178 (Jaysonn: "the king boo should not disappear thats fake he should just take a hit and then vanish to a new spot ... once we hit king boo 3 more
/// times there's 3 of them ... a way to tell them apart ... every time king boo gets hit he should be teleporting away once the hit animation is done to
/// give the fight a 'reset' feel"): a hit on a COPY is the same tumble and no damage (CopyHit); MaxCopies 2 (three of him by the sixth hit); the TELL
/// (CopyTell: the copy's crown CopyCrownColor instead of gold, or no fangs, or both, or none); and after every hurt -- his and a copy's -- the teleport to
/// another spot with a full gap there (TeleportAfterHit).
/// </summary>
public partial class SmsKingBooRematch : Node3D
{
    /// <summary>The ghost's screen mix (the casino boss's GhostOpacity; the game's ghost is 0.588).</summary>
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float GhostOpacity = 0.72f;
    /// <summary>198 (Jaysonn: "a setting for King Boo's animations to blend together a little better?"): every clip change CROSS-FADES over this long -- the pose he is in
    /// melts into the new clip's first frames (the wait into the laugh, the laugh into the dive, the hurt tumble into the wait, the laugh's own loop) instead of the
    /// game's hard cut between bcks. 0.25 s reads as one body moving; 0 = the hard cut as before; over 0.4 the quick clips (the hit, the lick's thrust) go mushy.</summary>
    [Export(PropertyHint.Range, "0,0.6,0.05")] public float ClipBlendSeconds = 0.25f;
    /// <summary>The glow quad's strength (x his opacity) and whether the twinkle stars show.</summary>
    [Export(PropertyHint.Range, "0,2,0.05")] public float Glow = 0.9f;
    [Export] public bool Twinkle = true;
    /// <summary>The hover: he bobs this many metres up and down over this many seconds (0 = still).</summary>
    [Export] public float HoverBob = 0.35f, HoverBobSeconds = 2.4f;
    /// <summary>173 (Jaysonn: "king boo sits kinda low and his tongue is going into the stage"): he hovers this many metres HIGHER than where he was put
    /// in the scene -- at the spawn, at every spot he teleports to (the lick's own LickHeight is not touched). 0 = as placed.</summary>
    [Export] public float HoverLift = 4.5f;   // 3 m left the tongue's tip 0.8 m into the floor; 4.5 puts it about 0.75 m over it (the bob is 0.35)
    [ExportGroup("The intro (174)")]
    /// <summary>174 (Jaysonn: "king boo appears just like the casino fight, laughs and then goes underground and the fight starts"): the intro's SURFACE --
    /// he rises out of the floor (from IntroSinkDepth under it, over IntroRiseSeconds, the appear clip and voice, as the casino boss surfaces from his table),
    /// LAUGHS for IntroLaughSeconds (the laugh clip looping, the long laugh voice), SINKS back (over IntroSinkSeconds, the vanish voice) and is hidden. The
    /// fight then starts with SpawnRising: up out of the floor at his spot over SpawnRiseSeconds, no second laugh.</summary>
    [Export] public float IntroRiseSeconds = 1.6f, IntroLaughSeconds = 3f, IntroSinkSeconds = 1.1f, IntroSinkDepth = 7f, SpawnRiseSeconds = 1.2f;   // 186: the laugh 3.4 -> 4.6 (the chuckle's run); 193 (Jaysonn: "the laugh doesn't have to be that long"): 4.6 -> 3 (192's climb runs over whatever the length is)
    /// <summary>182 (Jaysonn: "during the intro king boo gets stuck on the floor for a second; make the appear animation clean and smooth"): the appear clip
    /// (btelesa_appear, 0.97 s) is itself a LEAP -- the body spins up from 17 m under the node to 11 m over it by 0.33 s and lands back at the node by 0.6 s --
    /// and 174's node, lerping up from 7 m under the floor meanwhile, let the body pop out, fall back under the stone and sit at floor level for half a second
    /// before the lerp caught up. Now the node waits AT the hover height and the clip's own leap is the rise: the body comes up from 9 m under the floor and
    /// lands at the hover in one arc, the clip at IntroAppearClipSpeed (0.6: 1.6 s), its overshoot over the hover damped by RiseLeapDamp (0 = the clip's full 11 m
    /// leap, 1 = none: the node moves down to meet it). The same at the fight's spawn. IntroRiseSeconds / SpawnRiseSeconds only time the old lerp when the clip
    /// is missing.</summary>
    [Export(PropertyHint.Range, "0.2,2,0.05")] public float IntroAppearClipSpeed = 0.6f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float RiseLeapDamp = 0.6f;
    /// <summary>The laugh clip carries his body 3.6 m up over his node (the crown 9.6): through the intro's laugh the node is lowered by that (smoothed over
    /// IntroLaughHoldSeconds, so the clip's own bounce stays) and his body laughs at the hover height, where it rose to.</summary>
    [Export] public bool IntroLaughBodyHeld = true;
    [Export] public float IntroLaughHoldSeconds = 0.35f;
    /// <summary>180 (Jaysonn: "in the cutscene when king boo goes down use the animation of him hiding, and give it effects, let's make it look good"): the
    /// intro's sink is his HIDE clip (IntroDiveClip, btelesa_hide: the body leaps 6 m up and plunges 10 m down through the floor in 0.72 s) at IntroDiveClipSpeed,
    /// the node held at the laugh height -- with effects: a burst of his twinkle stars at the top of the leap (DiveSparkles), his glow swelling DiveGlowBoost x
    /// through the leap, and as the body comes down to DiveSplashAt over the floor a FLASH of light on the stone (DiveFlashSize m, DiveFlashSeconds), a RING of
    /// ghost mist racing out along the floor (DiveRingPuffs), a SPLASH of it thrown up (DiveSplashPuffs), a shock ring (DiveShockRing), the camera kicked (DiveShake)
    /// and the vanish voice at that moment; everything in DiveColor. "" = 174's plain sink (the wait clip lowered over IntroSinkSeconds).</summary>
    [Export] public string IntroDiveClip = "btelesa_hide";
    [Export(PropertyHint.Range, "0.3,2,0.05")] public float IntroDiveClipSpeed = 0.85f;
    [Export(PropertyHint.Range, "0,120,1")] public int DiveSparkles = 80, DiveRingPuffs = 44, DiveSplashPuffs = 36;
    [Export] public float DiveSplashAt = 2.2f, DiveFlashSize = 16f, DiveFlashSeconds = 0.45f, DiveShake = 0.6f;
    /// <summary>181: the flashes' and the rise glow's brightness over the colour (an additive quad brighter than its texture: 1 = the colour as is; the first shots' flashes
    /// read as a faint ring on a 6 m ghost).</summary>
    [Export(PropertyHint.Range, "0.5,4,0.1")] public float FlashBoost = 1.8f;
    [Export(PropertyHint.Range, "1,6,0.1")] public float DiveGlowBoost = 2.5f;
    [Export] public bool DiveShockRing = true;
    [Export] public Color DiveColor = new Color(0.8f, 0.7f, 1f);
    [ExportGroup("The rise and the teleports (181: effects)")]
    /// <summary>181 (Jaysonn: "we also need effects of him coming from the ground; we basically want everything to have some type of effect, make it flashy"): the
    /// RISES -- the intro's surface and the fight's spawn -- from the rise's start a GLOW pulses on the stone where he will come up (RiseGlowSize m at RiseGlowHz,
    /// growing) with wisps of ghost mist seeping up out of it (RiseWisps a second), and as the body breaks the floor (its middle RiseBreakAt m under it) the
    /// dive's splash in reverse: a FLASH there, a SHOCK RING, a RING of mist along the floor, a SPLASH of it thrown up, the camera kicked (RiseShake), his glow
    /// swelling RiseGlowBoost x and settling, and twinkle stars SHED off his body till he is up (RiseSparkles a second; 182: flung out and down, falling, not 181's column up past his crown). The TELEPORTS (TeleportFx) -- a
    /// poof of ghost mist and a burst of stars at his body as he fades out, and a flash, a poof and stars where he fades in (a copy's arrival, the found-out copy's
    /// fade and his split the same), TeleportPuffs / TeleportSparkles each, TeleportFxScale against the dive's sizes. All in DiveColor. Off: 174's bare rises
    /// and 162's bare fades.</summary>
    [Export] public bool RiseFx = true;
    /// <summary>The TELL before a rise: the glow and the wisps alone on the stone this long before the appear clip starts and he comes up (the appear clip's own first
    /// fifth of a second rushes the body up from 23 m under; without the tell the glow had 0.19 s). 0 = he comes up at once.</summary>
    [Export(PropertyHint.Range, "0,3,0.05")] public float RiseTellSeconds = 0.7f;
    [Export] public float RiseGlowSize = 14f, RiseGlowHz = 3f, RiseBreakAt = 2.2f, RiseShake = 0.5f;
    [Export(PropertyHint.Range, "1,6,0.1")] public float RiseGlowBoost = 2f;
    [Export(PropertyHint.Range, "0,120,1")] public int RiseWisps = 30, RiseSparkles = 50;
    [Export] public bool TeleportFx = true;
    [Export(PropertyHint.Range, "0,80,1")] public int TeleportPuffs = 20, TeleportSparkles = 30;
    /// <summary>182: the arrival's stars (they clustered at his crown -- Jaysonn: "not a fan of the crown sparkle effect"); off = the flash and the poof alone.</summary>
    [Export] public bool TeleportInStars = false;
    [Export(PropertyHint.Range, "0.2,2,0.05")] public float TeleportFxScale = 1f;
    [ExportGroup("The end: the defeat and the Shine (183)")]
    /// <summary>183 (Jaysonn: "lets do the ending of the fight also spawning the shine and such"): his DEFEAT and the SHINE. The last hit's hurt ends in the down clip
    /// (btelesa_down, 3.3 s: a spinning tumble that climbs 2.6 m by its end) with DeathFx -- his body blinking the hurt flash (DeathBlinkHz), ghost mist boiling off
    /// him (DeathMist alive) and twinkle stars shed off him (DeathSparkles alive), his glow swelling to DeathGlowBoost x, the crown flaring white, quicker and quicker,
    /// the camera rumbling (DeathRumble every 0.4 s), the node SINKING DeathSink m so the tumble ends low -- and at the clip's end he BURSTS: a flash DeathFlashSize m across, a shock ring on the floor under him, a
    /// poof of DeathPuffs and a burst of DeathStars (DeathFxScale x the dive's sizes), the camera kicked (DeathShake), an afterglow fading over
    /// DeathAfterglowSeconds where he was; Gone, Defeated (the supply stops). ShineDelaySeconds after the burst -- 223: after his effects are OUT (ShineAfterDeathFx: the
    /// afterglow is the last, 2.5 s) -- the SHINE, and the cut to the death camera with it: the ShineScene (empty =
    /// res://shine_sprite.tscn) added beside him, placed at ShineSpawnPoint, else ShineOverFloor m over the floor at the arena's middle, its back to Mario (so
    /// its demo camera, before it, looks past it at him); it materialises in a GOLDEN flash and stars (ShineFlashSize, ShineStars, ShineFlashColor) and appears
    /// the game's way (makeShineAppearWithDemo: TShine::appearWithDemo, ShineSprite's SmsAppear) FROM where he burst (ShineFromBurst; else at its spot) -- up
    /// 2 s, a loop down onto its spot, the settle -- with the demo (ShineDemo: the world
    /// frozen, the Shine's camera watching from ShineCamOffset at ShineCamFov -- 221: the wide shot instead, DeathCamWide; ShineDemoTicks 600 = 5 s: up 2, the loop 1,
    /// the settle 2, Petey's (183 had 720); 0 = the Shine scene's own 1200, 10 s; 223: his effects are over before it -- ShineAfterDeathFx off: they run on through it). ShineExitLevel /
    /// ShineExitScene set the Shine's exit (the save card, then that scene); off = the Shine scene's own (the card, control back). The casino boss's Shine came
    /// 240 ticks into his down, at his point, no burst.</summary>
    [Export] public bool DeathFx = true;
    /// <summary>He SINKS this far through the down clip (smoothed; the body kept 2 m over the floor), so the tumble ends low and the burst and the Shine's flight are
    /// down by the floor, not 13 m up in the black sky (the clip itself climbs 2.6 m). 0 = he dies where he hovered.</summary>
    [Export(PropertyHint.Range, "0,12,0.5")] public float DeathSink = 6f;
    [Export(PropertyHint.Range, "0,30,0.5")] public float DeathBlinkHz = 8f;
    [Export(PropertyHint.Range, "0,120,1")] public int DeathMist = 40, DeathSparkles = 40;
    [Export(PropertyHint.Range, "1,6,0.1")] public float DeathGlowBoost = 3f;
    [Export] public bool DeathCrownFlare = true;
    [Export(PropertyHint.Range, "0,2,0.05")] public float DeathRumble = 0.25f, DeathShake = 1.2f;
    [Export] public float DeathFlashSize = 30f, DeathFlashSeconds = 0.6f, DeathAfterglowSeconds = 2.5f;
    [Export(PropertyHint.Range, "0,200,1")] public int DeathPuffs = 60, DeathStars = 150;
    [Export(PropertyHint.Range, "0.2,3,0.05")] public float DeathFxScale = 1.6f;
    [Export] public PackedScene ShineScene;
    [Export] public Node3D ShineSpawnPoint;
    /// <summary>223 (Jaysonn: "i want king boo to full die animations and all like effects then the shine cutscene plays"): the Shine (and the cut to the death
    /// camera) WAIT for his death to play out -- the burst's flash, afterglow, shock ring, puffs and stars and the down's mist and twinkle emitters all gone --
    /// then ShineDelaySeconds more; off = 183's: ShineDelaySeconds after the burst, his effects running on under the Shine's demo.</summary>
    [Export] public bool ShineAfterDeathFx = true;
    /// <summary>224: the Shine is MADE AHEAD -- instanced, hidden and without its pickup zone, at the fight's start (its scene's instantiate and its _Ready, the
    /// effects and their materials, ran 100 ms on the main thread: a 135-ms frame right at the cut) -- so the frame it appears on only places and shows it.
    /// Off = made on that frame, as before.</summary>
    [Export] public bool ShineMadeAhead = true;
    [Export] public float ShineOverFloor = 1.2f, ShineDelaySeconds = 0.5f;   // 223: 0.5 after the effects (183: 1 s after the burst)
    [Export] public bool ShineFromBurst = true, ShineDemo = true;
    [Export(PropertyHint.Range, "0,2400,10")] public int ShineDemoTicks = 600;   // 221: Petey's -- biancoBoss's boss Shine camera (ボスシャインカメラ) runs 600: up 2 s, the jump 1 s, the settle 2 s (183 had 720: a 2-s glide)
    /// <summary>The Shine's demo camera (221: only with DeathCamWide off): where it sits in the Shine's placed frame (x right, y up, z forward -- its back is to Mario, so the camera looks past the Shine at him)
    /// and its field of view. The Shine scene's own (0, 2.5, 7) at 75 degrees showed a speck in the black sky through the flight from 13 m up. Zero = the Shine's own.</summary>
    [Export] public Vector3 ShineCamOffset = new Vector3(0f, 7f, 9f);
    [Export(PropertyHint.Range, "0,120,1")] public float ShineCamFov = 40f;
    /// <summary>The Shine's appearance: a GOLDEN flash (ShineFlashSize m) and a burst of golden stars (ShineStars) where it materialises.</summary>
    [Export] public float ShineFlashSize = 12f;
    [Export(PropertyHint.Range, "0,200,1")] public int ShineStars = 60;
    [Export] public Color ShineFlashColor = new Color(1f, 0.85f, 0.35f);
    [Export] public bool ShineExitLevel = false;
    [Export(PropertyHint.File, "*.tscn")] public string ShineExitScene = "";
    /// <summary>217 (Jaysonn: "can you make the shine bigger when it spawns? its very samll right now"): the Shine's size over its scene's own (the game's 1.5 m star:
    /// small in this 60-m arena) -- its glow, light and sparkles grow with it, its centre kept at the normal one's height over the floor (so Mario still walks into
    /// it; with ShineSpawnPoint the root goes where that node is), its demo camera looking at the bigger star's middle. In his hands at the shine-get it is the
    /// game's size again (the get demo drives it at the game's scale). 1 = the scene's own.</summary>
    [Export(PropertyHint.Range, "0.5,5,0.1")] public float ShineScale = 2.5f;
    /// <summary>221 (Jaysonn: "when king boo dies the cam pans out like petey phriana when he dies. where its almost looking at the entire aerna then the shine jumps
    /// to the middle"; 222: "it shouldnt pan it should cut"; 223: "king boo to full die animations and all like effects then the shine cutscene plays"): once his
    /// death has played out (ShineAfterDeathFx) the camera CUTS to a wide shot of the arena as the Shine comes -- out from the middle along the gameplay
    /// camera's own bearing: DeathCamDistance m out, DeathCamHeight m over the floor, looking at DeathCamLookUp m over the middle, DeathCamFov
    /// degrees (42 / 22 / 3 / 50: 97 % of the floor in frame, 24 degrees down, and the Shine's whole flight -- a burst anywhere out to 0.9 of the floor's radius,
    /// its loop up to half that distance high, the star's top and bottom -- in frame: tools/projectshine/rematch157/widecam221b.py; a burst farther out pulls the
    /// shot back along its line of sight till the flight fits, DeathCamFit). It holds there while the Shine comes out where he burst and jumps to the middle (the
    /// Shine's own demo camera off), and when the Shine's demo is over it CUTS back to the gameplay camera -- as the game cuts to and from Petey's placed Shine
    /// camera. DeathCamPanSeconds / DeathCamBackSeconds over 0 = 221's eased pan out and glide back instead. Off = 183's: the Shine's own camera
    /// (ShineCamOffset / ShineCamFov).</summary>
    [Export] public bool DeathCamWide = true;
    [Export(PropertyHint.Range, "10,80,0.5")] public float DeathCamDistance = 42f;
    [Export(PropertyHint.Range, "2,40,0.5")] public float DeathCamHeight = 22f;
    [Export(PropertyHint.Range, "-5,10,0.1")] public float DeathCamLookUp = 3f;
    [Export(PropertyHint.Range, "20,90,1")] public float DeathCamFov = 50f;
    [Export(PropertyHint.Range, "0,5,0.05")] public float DeathCamPanSeconds = 0f, DeathCamBackSeconds = 0f;   // 222: cuts (221 had 1.2 / 0.8)
    [ExportGroup("")]
    /// <summary>He turns to face Mario while he waits, at this many radians a second.</summary>
    [Export] public bool FaceMario = true;
    [Export] public float TurnSpeed = 3f;
    /// <summary>The long laugh at the laugh clip (through SmsActorSound: his sound node) and its gain -- the fight's laughs (a spawn with no intro, a copy
    /// found out, the smash's second laugh is its own).</summary>
    [Export] public string LaughSe = "MSD_SE_BS_TELESA_V_LAUGH1";
    [Export] public float LaughDb = 0f;
    /// <summary>186 (Jaysonn: "make the beginning laugh more high pitched like in the casino fight"): the INTRO's laugh is the casino intro's CHUCKLE --
    /// MSD_SE_BS_TELESA_V_LAUGH1B, the 6 s "hee-hee-hee" the casino plays whole as the roulette seats (its syllables climb 310 -> 360 Hz; the long laugh
    /// V_LAUGH1 sits flat at 300) -- at IntroLaughPitch (1 = as the casino plays it; over 1 higher and quicker), and it is CUT at his dive (IntroLaughStopsAtDive;
    /// off: it plays out over the dive and the splash). IntroLaughSeconds 3.4 -> 4.6 to hear the chuckle's run before the dive. "" = LaughSe as before.</summary>
    [Export] public string IntroLaughSe = "MSD_SE_BS_TELESA_V_LAUGH1B";
    [Export(PropertyHint.Range, "0.5,2,0.05")] public float IntroLaughPitch = 1f;
    [Export] public bool IntroLaughStopsAtDive = true;
    /// <summary>192 (Jaysonn: "have King Boo's laugh start low but then get higher in the intro cutscene; it doesn't need to scale back low"): the chuckle's pitch
    /// RISES through the laugh -- from IntroLaughPitchFrom to IntroLaughPitchTo (both x IntroLaughPitch) over IntroLaughSeconds, evenly in pitch (each second the
    /// same musical step: 0.75 -> 1.15 is seven semitones, a low chuckle climbing to a shade over the casino's own; 192's 0.7 -> 1.4 was an octave, shrill at the top), never back down. The clip itself climbs 308 -> 360 Hz
    /// over its first two seconds, then dips to 330 and wobbles; the ramp carries it over that. Higher is quicker too (the pitch scale), so the laugh gathers pace
    /// as it climbs. Both 1 = 186's flat laugh.</summary>
    [Export(PropertyHint.Range, "0.4,2,0.05")] public float IntroLaughPitchFrom = 0.75f, IntroLaughPitchTo = 1.15f;   // 194 (Jaysonn: "the laugh is too high pitched at the end"): 0.7 -> 1.4 (an octave) was shrill at the top; now it ends a shade over the casino's own
    /// <summary>192: the intro laugh's pitch now (From at the laugh's start, To at the dive).</summary>
    public float IntroLaughPitchNow => Mathf.Clamp(IntroLaughPitch, 0.25f, 4f) * IntroLaughPitchFrom * MathF.Pow(Mathf.Max(0.05f, IntroLaughPitchTo) / Mathf.Max(0.05f, IntroLaughPitchFrom), State == Phase.IntroLaugh ? Mathf.Clamp(_introT / Mathf.Max(0.05f, IntroLaughLength), 0f, 1f) : 0f);
    /// <summary>197: the intro laugh's length THIS time -- the intro director's trim to the music's cue (SmsRematchIntro.FightCueSeconds), else IntroLaughSeconds. NaN = the knob.</summary>
    public float IntroLaughOverride { get; set; } = float.NaN;
    public float IntroLaughLength => float.IsNaN(IntroLaughOverride) ? IntroLaughSeconds : Mathf.Max(0.3f, IntroLaughOverride);
    /// <summary>197: a clip's played length in seconds (its bck frames at 60; the clip's own length when the table has no row).</summary>
    public float ClipSeconds(string name) => Clips.TryGetValue(name, out var c) ? c.frames / 60f : (_anim != null && _anim.HasAnimation(name) ? (float)_anim.GetAnimation(name).Length : 1f);
    /// <summary>197: the intro's FIXED beats after the look-about and before the fight, for the director's cue: the rise tell, the appear clip's rise at its speed, the
    /// dive clip at its speed (IntroSinkSeconds when there is no dive clip) -- the laugh and the look-about are the trimmable rest.</summary>
    public float IntroFixedSeconds => RiseTellSeconds + ClipSeconds(BckAppear) / Mathf.Max(0.1f, IntroAppearClipSpeed)
        + (!string.IsNullOrEmpty(IntroDiveClip) && _anim != null && _anim.HasAnimation(IntroDiveClip) ? ClipSeconds(IntroDiveClip) / Mathf.Max(0.1f, IntroDiveClipSpeed) : IntroSinkSeconds);
    [Export] public bool Blink = true;
    [Export] public Texture2D EyeOpen, EyeHalf, EyeClose;
    [ExportGroup("The fight (158)")]
    /// <summary>The hits that take him down; each Bomb Boo burst on him takes one (177: 9 -- a copy of him every CopyEveryHits of them).</summary>
    [Export(PropertyHint.Range, "1,12,1")] public int HitPoints = 9;
    /// <summary>The hurt: 177 (Jaysonn: "make king boo do his flip when hurt like in the normal fight") -- the casino boss's spicy-hit reaction
    /// btelesa_damage (he tumbles head over heels, drops and bounces back up over 3.6 s) at HurtClipSpeed; "" = the short flinch btelesa_hit with
    /// HurtTurns full turns about himself. Then the flash's colour, and how much more solid he gets per hit (the casino boss's +30 / 255 is 0.12).</summary>
    [Export] public string HurtClip = "btelesa_damage";
    [Export(PropertyHint.Range, "0.5,3,0.05")] public float HurtClipSpeed = 1.25f;
    [Export] public float HurtTurns = 0f;
    [Export] public Color HurtFlash = new Color(1f, 0.35f, 0.35f);
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float OpacityPerHit = 0.1f;
    /// <summary>The hurt's sound (the casino boss's damage cry) and the down's.</summary>
    [Export] public string HurtSe = "MSD_SE_BS_TELESA_DAMAGE", DownSe = "MSD_SE_BS_TELESA_DOWN";
    /// <summary>159: his BODY for the swung Bomb Boo -- a cylinder about jnt_body: this radius, this far under the joint to this far over it (metres; the
    /// game's body hit cylinder is 350 u across, 550 tall from 200 under the joint: 4.7 / 2.7 / 4.7). A little inside the visible body.</summary>
    [Export] public float HitRadius = 4f, HitBelow = 2.5f, HitAbove = 4.5f;
    [ExportGroup("The copies (177)")]
    /// <summary>177 (Jaysonn: "every 3 hits king boo will make a copy of himself like in luigis mansion 3 and will attack alongside the other one but mario
    /// has to hit the real one for it to do any damage. this king boo however tends to stay away and more known to throw attacks not as fast or commonly"):
    /// as he recovers from every CopyEveryHits-th hit (0 = never) a COPY of him splits off (a flash; it fades in at a far spot, laughing) while fewer than
    /// MaxCopies are out. A copy looks and moves like him but takes no hits -- a Bomb Boo swung into it bursts for nothing and the copy is REVEALED (a laugh,
    /// it fades away). It keeps CopyMinFromMario metres from Mario (its spots), waits CopyGapScale times longer between moves, and only THROWS: the lava
    /// balls (CopyFireballs) and the breath (CopyBreath) at CopySpeedScale of his speed -- no lick, smash, lightning or wind. His defeat takes the copies with him.</summary>
    [Export(PropertyHint.Range, "0,9,1")] public int CopyEveryHits = 3;
    [Export(PropertyHint.Range, "0,4,1")] public int MaxCopies = 2;   // 178: "once we hit king boo 3 more times there's 3 of them"
    [Export] public float CopyMinFromMario = 13f, CopyGapScale = 1.3f, CopySpeedScale = 0.75f, CopyFadeSeconds = 0.7f;   // 196: CopyGapScale 1.7 -> 1.3 (the copies move about more)
    /// <summary>196 (Jaysonn: "let the other fake Boos teleport around -- they tend to blob up and it makes the fight clustered for the wrong reason"): a copy's move is a
    /// TELEPORT to another spot by this chance (0.6; 177's 0.3), a throw otherwise; and every spot any of him picks -- his hops and the copies' -- keeps SpotMinFromBoos
    /// (11 m: his body is 9 m across) from every other King Boo, where it stands or where it is going. The copies wait CopyGapScale 1.3 x his gap (1.7 before).</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float CopyRepositionChance = 0.6f;
    [Export(PropertyHint.Range, "0,25,0.5")] public float SpotMinFromBoos = 11f;
    [ExportGroup("The split's cutscene (188)")]
    /// <summary>188 (Jaysonn: "a little cutscene that plays when he splits into two, and three, showing three of him will now be attacking; make it King Boo
    /// themed"): as the hurt that earns a copy ends, the world FREEZES (the Shine demo's stop-the-world: Mario, the Boos, the supply held; he, his copies and
    /// the cutscene run on) and a cutscene camera cuts to a LINE across the arena's middle, facing Mario: he vanishes in a poof where he was and comes back at
    /// his place in the line out of a boil of violet-black ghost mist, laughing; his copies come in beside him one by one, each in a flash and the mist
    /// (SplitCopyEvery apart; a copy already out teleports to its place), SplitLineSpacing apart; then all of them turn on Mario and LAUGH TOGETHER (the copies
    /// a shade off his pitch, SplitLaughSpread: a chorus of him), a card line over it only when SplitLine2 / SplitLine3 have text (189: none -- "no need for the
    /// text on the screen"; the intro card's style), SplitHoldSeconds of the chorus, and the world runs again with every one of them in the wait at his place -- all attacking. A pressed
    /// skips to the end. The camera: Mario's side of the line, SplitCamDistance out (0 = far enough to frame the line), SplitCamHeight up. Off: 177's split
    /// (the copy fades in at a far spot while the fight runs).</summary>
    [Export] public bool SplitCutscene = true;
    [Export] public float SplitLineSpacing = 11f, SplitCopyEvery = 0.6f, SplitHoldSeconds = 1.3f, SplitCamDistance = 0f, SplitCamHeight = 4.5f, SplitCamFov = 50f;   // 11: his body is 9 m across (7 had two of him touching)
    /// <summary>190 (Jaysonn: "move them up a bit, they sit too low in the frame"): the point the camera looks at, this far over the hover height at the line's middle --
    /// lower = the line sits higher in the frame. 188's 4 (the middle of the laughing bodies) had them in the lower half; 1.5 lifts them about a seventh of the frame.</summary>
    [Export(PropertyHint.Range, "-4,8,0.5")] public float SplitCamLookUp = 1.5f;
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float SplitLaughSpread = 0.08f;
    /// <summary>189 (Jaysonn: "no need for the text on the screen"): the card lines are EMPTY -- no card; the scene is the line of him and the chorus. 188's lines were
    /// "Now there are TWO of me!" and "THREE of me! Can you keep up, Mario?" -- put text here for the card back.</summary>
    [Export] public string SplitLine2 = "", SplitLine3 = "";
    [Export] public Color SplitMistColor = new Color(0.88f, 0.92f, 1f);     // 193 (Jaysonn: "the purple smoke -- I want that white so it looks ghostly"): a cold white; 188's violet (0.48, 0.22, 0.62) before
    [Export(PropertyHint.Range, "0,200,1")] public int SplitMistPuffs = 70;
    /// <summary>194 (Jaysonn: "the effects could be a little better for the splitting cutscene when he spawns 2 and 3"): each of him now GATHERS out of the mist --
    /// SplitGatherPuffs puffs from a shell SplitGatherRadius out are drawn into his middle over SplitFadeSeconds while the body fades in FLICKERING (SplitFlicker,
    /// 0 = a plain fade), a ring of cold light (SplitRing: to SplitRingRadius over SplitRingSeconds) races out over the stone under him with a glow there, and the
    /// moment he is solid the mist BOILS back out of him (SplitMistPuffs, as before) with a flash: the pop. 188-193: the arrival's teleport flash and the boil only.</summary>
    [Export(PropertyHint.Range, "0.2,1.5,0.05")] public float SplitFadeSeconds = 0.7f;
    [Export(PropertyHint.Range, "0,400,1")] public int SplitGatherPuffs = 160;
    [Export(PropertyHint.Range, "0,120,1")] public int SplitGatherStars = 40;
    [Export(PropertyHint.Range, "3,20,0.5")] public float SplitGatherRadius = 10f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float SplitFlicker = 0.3f;
    [Export] public bool SplitRing = true;
    [Export] public float SplitRingRadius = 13f, SplitRingSeconds = 0.8f;
    [Export] public bool SplitSkipAllowed = true;
    /// <summary>203 (Jaysonn: "when the cutscene plays for phase 2 and 3, all the attacks go away"): as the cutscene cuts in, EVERY attack live in the arena is gone
    /// at once -- his and his copies' thunder walls, tornados, fireballs, breath, the Bomb Boos he spat (in the air, or a dud on the floor), the Boo circle, the
    /// Boolossi and their wave, and the ice on Mario (he thaws) -- under the cut, no effects. The supply's Boos and the one in Mario's hand are his and stay.
    /// Off: 188-202's -- the attacks freeze with the world and run on (a tornado stands through the whole cutscene) when it ends.</summary>
    [Export] public bool SplitClearsAttacks = true;
    /// <summary>208b (Jaysonn: "in the final phase it does the final boss skybox, the red fiery one"): at the split cutscene that opens the FINAL PHASE (FinalSkyAtSplit: 2 = the
    /// second split, three of him; 1 = the first) the arena's sky cross-fades over FinalSkyBlendSeconds from Luigi's Mansion's boss-arena sky to its FINAL-BOSS sky -- the
    /// King Boo / Bowser roof fight's vrKoopa dome (RematchSky.gdshader's final_blend: the dark red, the two fire layers, the red-to-yellow streaks). The fade runs
    /// through the cutscene (the cutscene node ticks it while the world is frozen) and on after it. Off = the boss-arena sky through the whole fight.</summary>
    [ExportGroup("The final phase's sky (208b)")]
    [Export] public bool FinalSky = true;
    [Export(PropertyHint.Range, "1,3,1")] public int FinalSkyAtSplit = 2;
    [Export(PropertyHint.Range, "0,10,0.1")] public float FinalSkyBlendSeconds = 2.5f;
    /// <summary>217 (Jaysonn: "when the boss is beat we should have the skybbox transform to somthing nice like the aurua lights to signal its now peacefull"): at his
    /// BURST the sky cross-fades over PeaceSkySeconds to the PEACEFUL NIGHT -- RematchSky.gdshader's peace_blend: a clear sky of stars and the northern lights --
    /// from whichever sky the fight was under (the boss arena's or the final fire), the arena's lights going calm with it (ArenaLighting's Peace colours: the
    /// flashes and the thunder gone), the embers out. The fade runs through the Shine's stop-the-world (he keeps processing till it is done). Off = the fight's sky stays.</summary>
    [Export] public bool PeaceSky = true;
    [Export(PropertyHint.Range, "0,20,0.5")] public float PeaceSkySeconds = 6f;
    [ExportGroup("")]
    [Export] public bool CopyFireballs = true, CopyBreath = true;
    /// <summary>178 (Jaysonn: "the king boo should not disappear thats fake he should just take a hit and then vanish to a new spot ... we do need a way to tell
    /// them apart. like one doesn't have teeth? or like the crown is not the right color?"): a hit on a copy is the TUMBLE and the teleport like the real one's,
    /// no damage, no reveal -- the TELL is how the player knows: Crown = the copy's crown is CopyCrownColor instead of gold; Teeth = the copy has no fangs;
    /// CrownAndTeeth = both; None = no tell (the shell game).</summary>
    public enum Tell { None, Crown, Teeth, CrownAndTeeth }
    [Export] public Tell CopyTell = Tell.Crown;
    [Export] public Color CopyCrownColor = new Color(0.62f, 0.7f, 0.95f);   // a cold silver-blue against the real gold
    /// <summary>178 (Jaysonn: "every time king boo gets hit he should be teleporting away once the hit animation is done to give the fight a 'reset' feel"):
    /// after the hurt he vanishes and arrives at another spot (a full gap there) instead of waiting where he was hit. Copies too.</summary>
    [Export] public bool TeleportAfterHit = true;
    /// <summary>191 (Jaysonn: "something else for when Mario hits the fake King Boos -- the bomb explodes and the Boo doesn't flinch and just laughs"): a hit on a
    /// COPY is the MOCK -- the bomb bursts on it as ever, but it does not flinch: no tumble, no flash; it LAUGHS (the laugh clip and CopyMockSe, the short high
    /// cackle) with a mocking rock (CopyMockRockDeg either way at 3 Hz) and bob, PHASES see-through -- flickering between solid and CopyMockOpacity of its solid at
    /// CopyMockFlickerHz, the crown with it (a ghost: nothing to hit), its crown flaring its own colour and fading, for CopyMockSeconds; then it teleports off to another spot (CopyMockTeleports; off: back to the wait
    /// where it is). The real one keeps the tumble -- the contrast is the tell. Off: 178's -- the copy tumbles and teleports like him.</summary>
    [Export] public bool CopyMock = true, CopyMockTeleports = true;
    [Export] public string CopyMockSe = "MSD_SE_BS_TELESA_V_LAUGH2";
    [Export(PropertyHint.Range, "0.4,3,0.05")] public float CopyMockSeconds = 1.3f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float CopyMockOpacity = 0.3f;
    [Export(PropertyHint.Range, "0,30,1")] public float CopyMockRockDeg = 9f;
    [Export(PropertyHint.Range, "0,1.5,0.05")] public float CopyMockBob = 0.45f;
    /// <summary>The see-through FLICKERS at this rate (the body phasing in and out, the crown with it); 0 = a steady dip (it read as a dark grey blob against the black sky).</summary>
    [Export(PropertyHint.Range, "0,12,0.5")] public float CopyMockFlickerHz = 5f;
    [ExportGroup("The fight (162): the teleports and the Lick")]
    /// <summary>The moves run at all (off: he waits as before, for the earlier passes' tests).</summary>
    [Export] public bool AttacksOn = true;
    /// <summary>Seconds from the fight's start (the wait begins) to his first move. 185 (Jaysonn: "speed up the rate at which king boo attacks, it feels too
    /// easy"): 3 -> 2.</summary>
    [Export] public float FirstAttackDelay = 2f;
    /// <summary>The gap between moves: this long with all his hit points, this long at his last one (the doc's pace: shorter per hit), give or take the jitter.
    /// 185: 3.5 / 1.8 / 0.6 -> 2.2 / 1.0 / 0.5 -- a move about every 2 s at the start, every second by the end.</summary>
    [Export] public float GapAtStart = 2.2f, GapAtLastHit = 1f, GapJitter = 0.5f;
    /// <summary>The chance a move is only a hop to another spot of the arena (no lick); the gap after one is half. 185: 0.3 -> 0.2 (more of the moves are attacks).</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float RepositionChance = 0.2f;
    /// <summary>The teleport: the fade out (the hide clip plays under it: its hop up; longer than 0.5 s shows its dive down too), the fade in, how far he
    /// settles down as he fades in, and the clip for the vanish ("" = none).</summary>
    [Export] public float TeleportOutSeconds = 0.4f, TeleportInSeconds = 0.3f, TeleportInDrop = 1.2f;
    [Export] public string TeleportClip = "btelesa_hide";
    [Export] public string VanishSe = "MSD_SE_BS_TELESA_DISAPPEAR", ArriveSe = "MSD_SE_BS_TELESA_V_APPEAR";
    /// <summary>The spots: at least this far from Mario, at most this far (0 = no cap; the arena is 26 m across), within this radius of the arena's middle
    /// (0 = three quarters of the floor's own).</summary>
    [Export] public float SpotMinFromMario = 7f, SpotMaxFromMario = 16f, SpotRadius = 0f;
    /// <summary>The lick's spot: this far in front of Mario along his face, his body joint this high over the floor (0 = his hover height). The roll's
    /// whip crosses the front 8.7 m out from his body joint, its tip 5.7 m under the body origin as it passes straight ahead and 6.4 m under at its lowest
    /// (frame 62, to his right): at 8.7 / 6.4 the TIP skims the floor through Mario's legs, 0.7 m up, and nothing of the tongue goes into the floor; the
    /// shaft behind it is 2.8 m up, over a jumping Mario. Lower, and the tongue comes through higher up him but its tip digs into the floor.</summary>
    [Export] public float LickDistance = 8.7f, LickHeight = 6.4f;
    /// <summary>The clip for the lick and its speed (btelesa_roll, 139 frames: he rears up to frame 30 with the tongue overhead, the tongue whips across the
    /// front over frames 50-66 as he plunges, the body is back at its height by 80; at 1.5 the crossing starts 0.56 s in). The hit window: the clip
    /// frames the tongue is live (the front crossing). The frame the clip is cut at for the wait.</summary>
    [Export] public string LickClip = "btelesa_roll";
    [Export] public float LickClipSpeed = 1.5f;
    [Export] public int LickHitFromFrame = 50, LickHitToFrame = 66, LickEndFrame = 80;
    /// <summary>How long he stays in the wait after the lick before he vanishes.</summary>
    [Export] public float AfterLickSeconds = 0.4f;
    /// <summary>The tongue's thickness for the hit (added to Mario's damage cylinder, 0.56 m across), and how much longer than the clips' the tongue is (163:
    /// "stick out more, not much but a bit"; 1 = as the clips have it).</summary>
    [Export] public float LickThickness = 0.35f;
    [Export(PropertyHint.Range, "1,1.6,0.01")] public float TongueStretch = 1.15f;
    /// <summary>The body joint never dips closer than this to the floor through the lick clip's plunge (the node lifts).</summary>
    [Export] public float BodyMinOverFloor = 2.6f;
    [Export] public string LickWindSe = "MSD_SE_BS_TELESA_V_ROLL", LickSe = "MSD_SE_BS_TELESA_LICK";
    [ExportGroup("The fight (164): the Boolossus Smash")]
    /// <summary>The smash runs at all; the attacks' WEIGHTS (166: the doc's near-random pick -- an attack's chance is its weight over the sum; 0 = never).</summary>
    [Export] public bool SmashOn = true;
    [Export(PropertyHint.Range, "0,10,0.5")] public float LickWeight = 1f, SmashWeight = 1f, LightningWeight = 1f, BreathWeight = 1f, FireballWeight = 1f, WindWeight = 1f;
    /// <summary>The Boolossus (Boolossus.tscn carries its own knobs: the height, the fall, the wave).</summary>
    [Export] public PackedScene SmashScene;
    /// <summary>The tell: his crown glows this colour (the doc: white) pulsing at this rate for this long before the first Boolossus; the clip he plays
    /// through the smash and its voice; how long he watches after the last Boolossus is out before the wait and the gap run again.</summary>
    [Export] public Color SmashCrownColor = new Color(1f, 1f, 1f);
    [Export] public float SmashTellSeconds = 0.6f, CrownPulseHz = 6f, SmashWatchSeconds = 1f;
    [Export] public string SmashClip = "btelesa_laugh";
    [Export] public string SmashSe = "MSD_SE_BS_TELESA_V_LAUGH2";
    /// <summary>165: how many Boolossi a smash makes, how far apart in time, and their approach directions (the first at random, the rest this many
    /// degrees on; SmashFromKingBoo: the first from where he stands).</summary>
    [Export(PropertyHint.Range, "1,6,1")] public int SmashCount = 3;
    [Export] public float SmashStagger = 0.5f, SmashSpreadDeg = 120f;
    [Export] public bool SmashFromKingBoo = true;
    /// <summary>The crown's SHINE (165c): the activation flash (this long at white-hot; again at each Boolossus's launch), the pulse quickening from
    /// CrownPulseHz to this many times it by the end of the tell, the sparkles (how many; 0 = none) rising out of the crown this far over its joint, and a
    /// halo quad (metres across; 0 = none -- it read as a bulb) with its strength. The crown goes solid while it shines (its ghost mix kept it dim).</summary>
    [Export] public float CrownFlashSeconds = 0.15f, CrownPulseQuicken = 2f;
    /// <summary>How bright the crown's shine is (x the shader's term; 1 = as tuned from the screenshots: a bright white-gold at the pulse's peak, white at the flash).</summary>
    [Export(PropertyHint.Range, "0,3,0.05")] public float CrownShine = 1f;
    [Export(PropertyHint.Range, "0,64,1")] public int CrownSparkles = 0;   // 182 (Jaysonn: "not a fan of the crown sparkle effect"): off -- the tell is the crown's colour and halo alone; 165c's 36 puts them back
    [Export] public float CrownFxLift = 1.2f;
    [Export] public float CrownGlowSize = 0f, CrownGlowStrength = 0.35f;
    [ExportGroup("The fight (166): Ghost Lightning")]
    /// <summary>The lightning runs at all; the crown's colour for it (the doc: red); the tell before the walls appear; the clip and voice through it; how
    /// long he watches after the last squeeze. The walls and the squeezes are SmsGhostLightning's own knobs (set here as a template).</summary>
    [Export] public bool LightningOn = true;
    [Export] public Color LightningCrownColor = new Color(1f, 0.12f, 0.08f);
    [Export] public float LightningTellSeconds = 0.5f, LightningWatchSeconds = 0.3f;
    [Export] public string LightningClip = "btelesa_laugh", LightningSe = "MSD_SE_BS_TELESA_V_LAUGH2";
    /// <summary>The walls: each one's length, height, how far apart they start, how fast each closes; the squeezes: how many, the tell before each closes,
    /// the pause after each, the axis's turn between them. 185 (Jaysonn: "I want the thunder walls to be jump-over-able more easily, but make them longer"):
    /// WallHeight 5 -> 2.2 (a held jump's apex is 3.03 m, his feet over a 2.2 m wall for 0.37 s of it; 5 m took a spin or triple jump), WallLength 10 -> 18
    /// (the open ends were 5 m off: a run out; now 9 m -- the dodge is the jump).</summary>
    [Export] public float WallLength = 18f, WallHeight = 2.2f, WallGap = 12f, WallSpeed = 8f;
    /// <summary>192 (Jaysonn: "the hit boxes for the electric wall are still off from the visuals"): how far up the wall the shock reaches, as a part of WallHeight --
    /// 0.85 (1.87 m): the bolts fade out over the quad's top quarter, so feet already over them were shocked at 1. (Also in the walls' code this pass: a wall is a
    /// slab about its drawn face -- a Mario who jumped a wall and came down outside it is clear; before, everything beyond a face counted.)</summary>
    [Export(PropertyHint.Range, "0.3,1.2,0.05")] public float WallHitTopK = 0.85f;
    [Export(PropertyHint.Range, "1,6,1")] public int SqueezeCount = 3;
    [Export] public float WarnSeconds = 0.45f, SqueezePause = 0.2f, SqueezeTurnDeg = 60f;
    [Export] public bool OneShockPerAttack = true;
    [ExportGroup("The fight (167): Ghost Breath")]
    /// <summary>The breath runs at all; the crown's colour for it (the doc: frosty blue); the tell before the cold comes; how long he watches after the sweep
    /// before the wait. The clip: the casino spit (btelesa_spit, 99 frames: the head rears back by frame 24 and thrusts at 48), played so that BreathHoldFrame
    /// lands at the tell's end and HELD there through the sweep, then let run on.</summary>
    [Export] public bool BreathOn = true;
    [Export] public Color BreathCrownColor = new Color(0.35f, 0.68f, 1f);
    [Export] public float BreathTellSeconds = 0.7f, BreathWatchSeconds = 0.3f;
    [Export] public string BreathClip = "btelesa_spit";
    [Export] public int BreathHoldFrame = 50;
    /// <summary>The sweep (SmsGhostBreath's knobs, set here as a template): the arc (degrees, about the way to Mario), its seconds, which way (as Mario sees it,
    /// facing King Boo; Random: a coin), how far under level the cold is aimed (degrees); the cold's speed (m/s), how long a puff of it lives (the reach is the two
    /// multiplied), a puff's size as it leaves the mouth and at its end (metres across); how high over the floor the cold reaches (a Mario whose feet are higher is
    /// over it: the jump); the freeze's seconds; one freeze per attack.</summary>
    [Export] public float SweepDeg = 150f, SweepSeconds = 1.6f;
    [Export] public SmsGhostBreath.Way SweepWay = SmsGhostBreath.Way.Random;
    [Export] public float BreathDownDeg = 10f, BreathSpeed = 22f, PuffSeconds = 1.5f, PuffSizeStart = 3f, PuffSizeEnd = 8f, BreathHitHeight = 1f, FreezeSeconds = 1f;
    /// <summary>199 (Jaysonn: "the breath a bit wider -- King Boo has a wide mouth, one point of contact looks off"): the mouth the cold leaves, metres across and tall
    /// (his body is 9 m across; the mouth some 5), and how many lanes the cold that freezes runs in across it (SmsGhostBreath's MouthWidth / MouthHeight / HitLanes).</summary>
    [Export(PropertyHint.Range, "0.5,9,0.1")] public float BreathMouthWidth = 4.5f;
    [Export(PropertyHint.Range, "0.2,4,0.1")] public float BreathMouthHeight = 1.4f;
    [Export(PropertyHint.Range, "1,7,1")] public int BreathHitLanes = 3;
    /// <summary>192 (Jaysonn: "the hit boxes for the freeze breath are still off from the visuals"): how much the mist SLOWS once it rolls on the floor (the puffs'
    /// collision friction, taken off their speed every frame they touch the stone) -- 0: the mist slides out with the cold that freezes, as the hit maths always
    /// had it; 0.15 (before) stalled the visible mist about 10 m out while the freeze ran on to 25 m, so Mario was frozen with the mist still 5-10 m behind him.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float BreathFloorDrag = 0f;   // 172: the puffs 3 -> 8 m (thicker); 187: the jump clears the cold at 1 m (1.2)
    /// <summary>173: the breath is aimed so the stream meets the floor this many metres out from the mouth whatever his height (he sits higher now; a fixed
    /// 10 degrees from a mouth 5 m up would pass over a Mario standing near him). 0 = BreathDownDeg as it is.</summary>
    [Export] public float BreathAimFloorAt = 10f;
    /// <summary>The breath's aim as last launched (degrees under level), for the drives.</summary>
    public float BreathDownDegUsed { get; private set; }
    [Export] public bool OneFreezePerAttack = true;
    [ExportGroup("The fight (168): Fireballs")]
    /// <summary>The fireballs run at all; the crown's colour for them (the doc: red -- a red-orange here, the lightning's red being deeper); the tell before
    /// the first ball; how long he watches after the volley before the wait; the clip (the casino spit, held at FireballHoldFrame through the volley).</summary>
    [Export] public bool FireballsOn = true;
    [Export] public Color FireballCrownColor = new Color(1f, 0.32f, 0.02f);
    [Export] public float FireballTellSeconds = 0.6f, FireballWatchSeconds = 0.4f;
    [Export] public string FireballClip = "btelesa_spit";
    [Export] public int FireballHoldFrame = 50;
    /// <summary>The pops (SmsFireballs' knobs, set here as a template; 172): how many POPS out of the crown and how many lava balls each, the time between
    /// pops, how high over the crown they lob (metres), how far about Mario they come down (metres; one of each pop lands within 1.5 m of him), how fast a
    /// landed ball rolls on (m/s), a ball's size at birth (metres across), how long one lives (shrinking to nothing), how high over the floor the fire reaches
    /// (a Mario jumping higher is over it), whether a ball bursts on Mario.</summary>
    [Export(PropertyHint.Range, "1,8,1")] public int FireballPops = 3;
    [Export(PropertyHint.Range, "1,8,1")] public int FireballPerPop = 3;
    [Export] public float FireballPopSeconds = 0.45f, FireballLobHeight = 6f, FireballScatter = 7f, FireballAimedWithin = 1.5f, FireballRollSpeed = 8f, FireballSize = 3f, FireballSeconds = 6f, FireballHitHeight = 1.6f;   // 173: 3 m balls (were 2)
    /// <summary>How many lava balls a Fireballs move pops in all.</summary>
    public int FireballCount => FireballPops * FireballPerPop;
    [Export] public bool FireballDiesOnHit = true;
    [ExportGroup("The fight (200): the Bomb Boo VOLLEY")]
    /// <summary>200 (Jaysonn: "one more attack involving boos somehow"): the volley runs at all and its weight among the attacks; the crown's colour for it (the
    /// Bomb Boo's eye yellow); the tell before the first Boo; how long he watches after the last leaves before the wait; the clip (the casino spit, held at
    /// VolleyHoldFrame through the spit, as the fireballs' is); whether a copy spits them too (its Boos are real: a dud of its is a weapon all the same).</summary>
    [Export] public bool VolleyOn = true;
    [Export(PropertyHint.Range, "0,10,0.5")] public float VolleyWeight = 1f;
    [Export] public Color VolleyCrownColor = new Color(1f, 0.82f, 0.2f);
    [Export] public float VolleyTellSeconds = 0.7f, VolleyWatchSeconds = 0.5f;
    [Export] public string VolleyClip = "btelesa_spit";
    [Export] public int VolleyHoldFrame = 50;
    [Export] public bool CopyVolley = false;
    /// <summary>The spit (SmsBooVolley's knobs, set here as a template): how many Bomb Boos out of the MOUTH, the time between them, how high over the mouth they
    /// lob (metres), how far about Mario they come down (metres; the first within VolleyAimedWithin of him, the rest anywhere out to VolleyScatter), the
    /// blast's reach on the floor and how high it reaches (Mario's feet higher are over it: the jump), whether the LAST one is a DUD -- a live Bomb Boo that
    /// floats where it lands for Mario to grab, at least VolleyDudMinFromMario from him -- and how long its fuse burns once down (0 = it never blasts on its own).
    /// 203 (Jaysonn: "let's not have a dud for King Boo's Bomb Boo attack"): VolleyDud is OFF -- every Boo he spits is lit and blasts where it lands; on = 200's dud.
    /// 229: the volley's wall (its WallInset inside the arena's edge) is measured from the STONE's edge (SmsArenaFloor.StoneRadius, 25.1 m), not the model's span (26.4).</summary>
    [Export(PropertyHint.Range, "1,8,1")] public int VolleyCount = 3;
    [Export] public float VolleySpacing = 0.4f, VolleyLobHeight = 9f, VolleyScatter = 5f, VolleyAimedWithin = 1f, VolleyBlastRadius = 3.5f, VolleyBlastHeight = 1.6f;
    [Export] public bool VolleyDud = false;
    [Export] public float VolleyDudSeconds = 6f, VolleyDudMinFromMario = 2.5f;
    [ExportGroup("The fight (201): the AMBUSH from below")]
    /// <summary>201 (Jaysonn: "lets do 1" -- the ambush from below): he SINKS where he stands (the hide clip, the fade, his poof -- AmbushSinkSeconds) and his glow
    /// POOL (the rise's glow and wisps, AmbushPoolSize m across) HUNTS Mario across the stone for AmbushHuntSeconds -- sliding toward his feet at AmbushHuntSpeed,
    /// AmbushHuntQuicken times that by the end -- then STOPS and flares for AmbushTellSeconds (the tell), and he ERUPTS there (the appear clip's leap through
    /// the floor, the rise's burst, kick and stars) with a shock RING racing out to AmbushRingRadius over AmbushRingSeconds: a Mario within it whose feet are
    /// under AmbushRingHeight is hit (the common enemy damage) -- a jump at the break clears it. He is up at the pool's spot (the ambush moves him), the wait.
    /// Under the floor he cannot be hit. The weight among the attacks.</summary>
    [Export] public bool AmbushOn = true;
    [Export(PropertyHint.Range, "0,10,0.5")] public float AmbushWeight = 1f;
    [Export] public float AmbushSinkSeconds = 0.5f, AmbushHuntSeconds = 3f, AmbushHuntSpeed = 6f, AmbushHuntQuicken = 1.6f, AmbushTellSeconds = 0.45f;
    [Export] public float AmbushPoolSize = 7f, AmbushRingRadius = 5f, AmbushRingSeconds = 0.45f, AmbushRingHeight = 1.6f;
    [Export] public Color AmbushRingColor = new Color(0.8f, 0.7f, 1f, 0.9f);
    [ExportGroup("The fight (201): the BOO CIRCLE (Super Mario World's ghost houses)")]
    /// <summary>201 (Jaysonn: "a circle of boos attack similar to how ghost houses used to have them in super mario world"): his laugh through the tell (the crown
    /// CircleCrownColor, CircleTellSeconds), then a RING of hotel Boos (SmsBooCircle: CircleCount places, CircleGap of them empty -- the gap) forms about Mario,
    /// CircleRadius out, CircleHeight over the floor, turning once every CircleRevSeconds; it holds CircleHoldSeconds, then CLOSES to CircleMinRadius by
    /// CircleSeconds (drifting after him at CircleDriftSpeed), and fades. A Boo touching Mario hurts him (the common enemy damage); out through the gap, or over
    /// (a Boo's top is CircleHeight + its half height). CircleWatchSeconds after the ring is up, the wait (the ring runs on its own; one at a time). A hit on him
    /// through it fades the ring.</summary>
    [Export] public bool CircleOn = true;
    [Export(PropertyHint.Range, "0,10,0.5")] public float CircleWeight = 1f;
    [Export] public Color CircleCrownColor = new Color(0.75f, 0.55f, 1f);
    [Export] public float CircleTellSeconds = 0.8f, CircleWatchSeconds = 0.5f;
    [Export] public string CircleClip = "";
    [Export(PropertyHint.Range, "3,16,1")] public int CircleCount = 8;
    [Export(PropertyHint.Range, "1,4,1")] public int CircleGap = 1;
    [Export] public float CircleRadius = 7f, CircleMinRadius = 1f, CircleHeight = 1.5f, CircleRevSeconds = 4f, CircleHoldSeconds = 2f, CircleSeconds = 8f, CircleDriftSpeed = 1f;
    private string CircleClipNow => string.IsNullOrEmpty(CircleClip) ? BckLaugh : CircleClip;
    [ExportGroup("The fight (169): Ghost Wind")]
    /// <summary>The wind runs at all; the crown's colour for it (the doc: pink, for now); the tell before the tornados rise; how long he watches after they
    /// have risen before the wait; the clip and voice through it.</summary>
    [Export] public bool WindOn = true;
    [Export] public Color WindCrownColor = new Color(1f, 0.45f, 0.8f);
    [Export] public float WindTellSeconds = 0.6f, WindWatchSeconds = 0.6f;
    [Export] public string WindClip = "btelesa_laugh", WindSe = "MSD_SE_BS_TELESA_V_LAUGH2";
    /// <summary>The tornados (SmsGhostWind's knobs, set here as a template): how many, how far out from the arena's middle they rise (x its radius), how far from
    /// Mario at least, how long they live; how fast they come at him (m/s) and how quickly they can turn (degrees a second); the funnel's height; the wind's reach
    /// about the axis; the fling's speed (the game's throw units: 140 lifts Mario 33 m) and how much of it is up; the grace after a fling.</summary>
    [Export(PropertyHint.Range, "1,8,1")] public int TornadoCount = 5;   // 195 (Jaysonn: "spawn 5 tornados"): 2 -> 5
    [Export] public float TornadoSpawnRadius = 0.7f, TornadoMinFromMario = 8f, TornadoSeconds = 7f, TornadoSpeed = 6f, TornadoTurnDegPerSec = 70f, TornadoHeight = 8f, TornadoRadius = 1.7f;
    /// <summary>195 (Jaysonn: "have the wind pattern go into the middle then back out while circling around the arena"): the tornados' PATTERN -- Spiral: the five rise
    /// evenly spaced on a ring TornadoSpawnRadius out and the ring CIRCLES the arena at TornadoSpeed along it (all one way, a random way each attack) while it
    /// breathes IN to TornadoInnerRadius (x the arena's: 0.12 of 26 m is 3 m -- they bunch at the middle without sitting on each other) and back OUT,
    /// TornadoSpiralCycles times over TornadoSeconds. Hunt: 169's -- each comes at Mario.</summary>
    [Export] public SmsGhostWind.Patterns TornadoPattern = SmsGhostWind.Patterns.Spiral;
    [Export(PropertyHint.Range, "0,0.6,0.01")] public float TornadoInnerRadius = 0.12f;
    [Export(PropertyHint.Range, "0.5,3,0.5")] public float TornadoSpiralCycles = 1f;
    [Export] public float FlingSpeed = 85f, FlingUpK = 0.75f, FlingGraceSeconds = 2f;
    [ExportGroup("The music and the mix (195)")]
    /// <summary>195 (Jaysonn: "lower the volume a bit and throw in the boss music now"): the BOSS MUSIC -- the casino King Boo's theme (MSD_BGM_BOSS, t_boss.com:
    /// 4.2 s in and a 95 s loop, rendered from the game's sequence) on the Music bus, filled into BossMusic by his sound node (set a stream by hand to use another),
    /// started at the cue BossMusicCue (197: the intro's first frame, Mario looking about; 196: the cut to the stance; 195: the fight) and FADED over BossMusicFadeSeconds
    /// as the last hit takes him down. BossMusicDb on top of the music's own level (SmsActorSound.MusicDb -4 and the game's volume for the track). Off: no music.</summary>
    [Export] public AudioStreamPlayer BossMusic;
    [Export] public bool BossMusicOn = true;
    /// <summary>197 (Jaysonn: "try a different song -- Antasma Final Battle; the music starts right when Mario looks around, and the fight starts within 7.5 s of the song
    /// starting to cue the proper music timing"): WHEN it starts -- LookAbout: the intro cutscene's first frame, Mario looking about for the laugh (the intro then trims
    /// its beats so the fight begins FightCueSeconds in: SmsRematchIntro); StanceCut: 196's, the cut to Mario in the battle stance as King Boo surfaces; Fight: 195's,
    /// his spawn rise after the dive. The fight is the last chance: with LookAbout or StanceCut set and no cutscene run, it starts at the fight.
    /// WHAT plays -- BossMusicStream (a stream set by hand), else the file BossMusicFile when it exists (the Antasma Final Battle mp3, 2:48, under
    /// assets/Sms/Sound/Music/), else the casino theme his sound node fills in (MSD_BGM_BOSS). A file or a hand-set stream loops (BossMusicLoop) back to
    /// BossMusicLoopOffset (7.5 s: past the song's quiet rise, to where it is going) and plays at SmsActorSound.MusicDb + BossMusicDb + BossMusicStreamDb (-1.3: the
    /// Antasma mp3 sits 1.2 dB under the casino theme's render -- RMS -19.5 against -18.3 dBFS -- so the two come out alike); the casino theme at its own level + BossMusicDb.</summary>
    public enum BossMusicCues { Fight, StanceCut, LookAbout }
    [Export] public BossMusicCues BossMusicCue = BossMusicCues.LookAbout;
    [Export] public AudioStream BossMusicStream;
    [Export] public string BossMusicFile = "res://assets/Sms/Sound/Music/AntasmaFinalBattle.mp3";
    [Export] public bool BossMusicLoop = true;
    [Export(PropertyHint.Range, "0,60,0.1")] public float BossMusicLoopOffset = 7.5f;
    [Export(PropertyHint.Range, "-24,12,0.1")] public float BossMusicStreamDb = -1.3f;
    [Export(PropertyHint.Range, "-24,12,0.5")] public float BossMusicDb = 0f;
    [Export(PropertyHint.Range, "0.1,6,0.1")] public float BossMusicFadeSeconds = 2f;
    /// <summary>195: the rematch's SOUND EFFECTS level -- every SMS actor's sounds (his voice and attacks, the Bomb Boos, the Boolossi) through SmsActorSound.MasterDb while
    /// this arena is loaded (put back when it unloads); Mario's own sounds are not his. -4: "a bit" under the music. 0 = 157-194's level.</summary>
    [Export(PropertyHint.Range, "-24,6,0.5")] public float SfxDb = -4f;
    [ExportGroup("")]
    [Export] public bool DebugPrint;
    /// <summary>192 (Jaysonn: "the hit boxes for the electric wall and freeze breath are still off from the visuals"): the hits DRAWN, to judge them in play --
    /// Mario's damage cylinder (green: 42 u across, 130 u tall from his feet, the game's), each lightning wall's slab (red: its length, WallHitTopK of its height,
    /// its thickness) while it stands, and every live puff of the breath's cold at its hit radius (red balls). A hit is a red volume touching the green one
    /// (the breath's only with his feet under BreathHitHeight). Off for play.</summary>
    [Export] public bool ShowHitBoxes;

    [Signal] public delegate void SpawnedEventHandler();
    [Signal] public delegate void LaughedEventHandler();
    [Signal] public delegate void ReadyForFightEventHandler();
    /// <summary>174: the intro's surface begins (he breaks the floor), and he has sunk back under it (the intro may start the fight).</summary>
    [Signal] public delegate void IntroSurfacingEventHandler();
    [Signal] public delegate void IntroSunkEventHandler();
    /// <summary>158: a Bomb Boo burst on him (the hit points left) / the last one took him down.</summary>
    [Signal] public delegate void HitEventHandler(int hpLeft);
    [Signal] public delegate void DefeatedEventHandler();
    /// <summary>183: the Shine is in the arena (the fight's end), appearing.</summary>
    [Signal] public delegate void ShineAppearingEventHandler(Node3D shine);
    /// <summary>177: a copy of him split off / 178: a copy took a (harmless) hit (both from the REAL one).</summary>
    [Signal] public delegate void CopyMadeEventHandler(SmsKingBooRematch copy);
    [Signal] public delegate void CopyHitEventHandler(SmsKingBooRematch copy);
    /// <summary>162: he has arrived at a spot; the lick's wind-up begins; the tongue is pulled back (whether it got Mario).</summary>
    [Signal] public delegate void TeleportedEventHandler();
    [Signal] public delegate void LickStartedEventHandler();
    [Signal] public delegate void LickedEventHandler(bool hit);
    /// <summary>164: the smash's tell begins; the Boolossus has landed (whether its body got Mario).</summary>
    [Signal] public delegate void SmashStartedEventHandler();
    [Signal] public delegate void SmashedEventHandler(bool hitMario);
    /// <summary>166: the lightning's tell begins; a squeeze has met (whether it shocked Mario).</summary>
    [Signal] public delegate void LightningStartedEventHandler();
    [Signal] public delegate void LightningSqueezedEventHandler(bool hitMario);
    /// <summary>167: the breath's tell begins; the sweep is over (whether the cold froze Mario).</summary>
    [Signal] public delegate void BreathStartedEventHandler();
    [Signal] public delegate void BreathedEventHandler(bool hitMario);
    /// <summary>168: the fireballs' tell begins; the last ball is out; a ball burned Mario; the last ball has died (the burns in all).</summary>
    [Signal] public delegate void FireballsStartedEventHandler();
    [Signal] public delegate void FireballsShotEventHandler();
    [Signal] public delegate void FireballBurnedEventHandler();
    [Signal] public delegate void FireballsDoneEventHandler(int burns);
    /// <summary>200: the volley's tell; the last Boo has left the mouth (how many); a blast on the floor (whether it got Mario); the volley is over (the hits).</summary>
    [Signal] public delegate void VolleyStartedEventHandler();
    [Signal] public delegate void VolleySpatEventHandler(int count);
    [Signal] public delegate void VolleyBlastedEventHandler(bool hitMario);
    [Signal] public delegate void VolleyDoneEventHandler(int hits);
    /// <summary>201: the ambush -- he sinks; the pool stops hunting (the tell); he erupts (whether the ring got Mario); he is up (the hits). The circle -- the tell; the ring made (its Boos); a Boo got Mario; the ring is gone (the hits).</summary>
    [Signal] public delegate void AmbushStartedEventHandler();
    [Signal] public delegate void AmbushTellEventHandler();
    [Signal] public delegate void AmbushEruptedEventHandler(bool hitMario);
    [Signal] public delegate void AmbushDoneEventHandler(int hits);
    [Signal] public delegate void CircleStartedEventHandler();
    [Signal] public delegate void CircleMadeEventHandler(int boos);
    [Signal] public delegate void CircleHitEventHandler();
    [Signal] public delegate void CircleDoneEventHandler(int hits);
    /// <summary>169: the wind's tell begins; the tornados have risen; a tornado flung Mario; the last tornado is gone (the flings in all).</summary>
    [Signal] public delegate void WindStartedEventHandler();
    [Signal] public delegate void WindSpawnedEventHandler(int count);
    [Signal] public delegate void FlungEventHandler();
    [Signal] public delegate void WindDoneEventHandler(int flings);

    public enum Phase { Hidden, Appear, Laugh, Wait, Hurt, Down, Gone, Vanish, Arrive, Lick, Smash, Lightning, Breath, Fireballs, Wind, IntroRise, IntroLaugh, IntroSink, Split, Mock, Volley, Ambush, Circle }
    public enum Move { None, Hop, Lick, Smash, Lightning, Breath, Fireballs, Wind, Volley, Ambush, Circle }
    public Phase State { get; private set; } = Phase.Hidden;
    /// <summary>The clip he is on (the assigned one while it is held paused: 167).</summary>
    public string Clip => _anim != null ? (string)(_anim.IsPlaying() ? _anim.CurrentAnimation : _anim.AssignedAnimation) : "";
    public bool IsSpawned => State != Phase.Hidden;
    public bool LaughPlayed { get; private set; }
    /// <summary>186: the intro's laugh sound (IntroLaughSe, else LaughSe); whether his sound node is playing it now (for the drives).</summary>
    public string IntroLaughSeNow => !string.IsNullOrEmpty(IntroLaughSe) ? IntroLaughSe : LaughSe;
    public bool IntroLaughPlaying => _sound != null && !string.IsNullOrEmpty(IntroLaughSeNow) && _sound.IsPlaying(IntroLaughSeNow);
    public int Hp { get; private set; } = -1;
    public int HitsTaken { get; private set; }
    /// <summary>177: this one is a COPY (takes no hits) of Original; the real one's copies out now, and made in all.</summary>
    public bool IsCopy { get; private set; }
    public SmsKingBooRematch Original { get; private set; }
    public int CopiesAlive { get { int n = 0; foreach (var c in _copies) if (c != null && IsInstanceValid(c) && c.State != Phase.Gone) n++; return n; } }
    public int CopiesMade { get; private set; }
    public IReadOnlyList<SmsKingBooRematch> Copies => _copies;
    private readonly List<SmsKingBooRematch> _copies = new(); private bool _copyPending, _revealing, _afterHit;
    /// <summary>178, for the drives: the hits this copy has taken (no damage), and whether its fangs are hidden / its crown re-coloured by the tell.</summary>
    public int CopyHitsTaken { get; private set; }
    public bool TeethHidden => _teethMat != null && IsCopy && (CopyTell == Tell.Teeth || CopyTell == Tell.CrownAndTeeth);
    public Vector3 CrownRestColor => _crownRest;
    private ShaderMaterial _teethMat;
    /// <summary>A Bomb Boo bursts on him while he waits or licks (not between spots, through the hurt, the down, the entrance).</summary>
    public bool CanBeHit => State == Phase.Wait || State == Phase.Lick || State == Phase.Smash || State == Phase.Lightning || State == Phase.Breath || State == Phase.Fireballs || State == Phase.Wind || State == Phase.Volley || State == Phase.Circle || (State == Phase.Ambush && _ambushStage == 3 && _riseBroke);   // 201: under the floor he is out of reach
    public bool IsAlive => State != Phase.Down && State != Phase.Gone;
    public int MaterialsCopied => _mats.Count;
    /// <summary>162, for the drives: the lick's stage (-1 none, 0 wind-up, 1 the whip live, 2 the clip's tail, 3 the wait after), the counts, the gap left,
    /// the last spot, his hover height over the floor, the arena's middle and radius as he read them, the tongue.</summary>
    public int LickStage => State == Phase.Lick ? _lickStage : -1;
    public int MovesMade { get; private set; }
    public int TeleportsMade { get; private set; }
    public int LicksMade { get; private set; }
    public int LickHits { get; private set; }
    public int LickMisses { get; private set; }
    public float GapNow => _gapT;
    public float LastGap { get; private set; }
    public bool LastMoveWasLick => LastMove == Move.Lick;
    public Move LastMove { get; private set; }
    /// <summary>164: the smashes made, how many landed ON Mario, the last Boolossus (while it lives), the crown's colour register now (yellow at rest).</summary>
    public int SmashesMade { get; private set; }
    public int SmashHits { get; private set; }
    public SmsBoolossus Boolossus => _boolossus != null && IsInstanceValid(_boolossus) ? _boolossus : null;
    public Vector3 CrownColorNow => _crownNow;
    public bool SmashLaunched => _smashLaunched;
    /// <summary>165: the crown's glow now (0 = dark, its colour), how many Boolossi this smash has launched, the last one's approach direction.</summary>
    public float CrownGlowK => _crownGlowK;
    public Color CrownGlowColor => _crownGlowColor;
    public bool CrownGlowShown => _crownGlow != null && _crownGlow.Visible;
    public bool CrownSparklesOn => _crownSpark != null && _crownSpark.Emitting;
    public float CrownShineK => _crownShineK;
    public int SmashLaunchedCount => _smashLaunchedCount;
    /// <summary>166: the lightnings made, the squeezes that shocked Mario, the one running (while it lives).</summary>
    public int LightningsMade { get; private set; }
    public int LightningHits { get; private set; }
    public SmsGhostLightning Lightning => _lightning != null && IsInstanceValid(_lightning) ? _lightning : null;
    public bool LightningLaunched => _lightningLaunched;
    /// <summary>167: the breaths made, the ones that froze Mario, the one running (while it lives), whether this breath's cold is out, the side it sweeps from (+1: Mario's left).</summary>
    public int BreathsMade { get; private set; }
    public int BreathHits { get; private set; }
    public SmsGhostBreath Breath => _breath != null && IsInstanceValid(_breath) ? _breath : null;
    public bool BreathLaunched => _breathLaunched;
    public int BreathSide => _breathSide;
    public bool BreathClipHeld => _breathClipHeld;
    /// <summary>168: the volleys made, the balls that burned Mario in all, the volley running (while it lives), whether this volley is out.</summary>
    public int FireballVolleys { get; private set; }
    public int FireballBurns { get; private set; }
    public SmsFireballs Fireballs => _fireballs != null && IsInstanceValid(_fireballs) ? _fireballs : null;
    public bool FireballsLaunched => _fireLaunched;
    /// <summary>200: the volleys made, the blasts that got Mario in all, the volley running (while it lives), whether this one's Boos are out of the mouth, the last dud.</summary>
    public int VolleysMade { get; private set; }
    public int VolleyHits { get; private set; }
    public SmsBooVolley Volley => _volley != null && IsInstanceValid(_volley) ? _volley : null;
    public bool VolleyLaunched => _volleyLaunched;
    public SmsBombBoo VolleyDudBoo { get; private set; }
    /// <summary>201: the ambushes made and their hits; the stage now (0 the sink, 1 the hunt, 2 the tell, 3 the erupt), the pool's floor point, how far through the hunt, the ring's radius now and
    /// whether it got him; the circles made and their hits, the circle running (while it lives), whether this one's ring is out.</summary>
    public int AmbushesMade { get; private set; }
    public int AmbushHits { get; private set; }
    public int AmbushStage => State == Phase.Ambush ? _ambushStage : -1;
    public Vector3 AmbushPool => _ambushPool;
    public float AmbushHuntK => AmbushHuntSeconds > 0f ? Mathf.Clamp(_ambushHuntT / AmbushHuntSeconds, 0f, 1f) : 1f;
    public float AmbushRingRadiusNow { get; private set; }
    public bool AmbushRingHit { get; private set; }
    public int AmbushBreaks { get; private set; }
    public int CirclesMade { get; private set; }
    public int CircleHits { get; private set; }
    public SmsBooCircle Circle => _circle != null && IsInstanceValid(_circle) ? _circle : null;
    public bool CircleLaunched => _circleLaunched;
    /// <summary>169: the winds made, the flings in all, the wind running (while it lives), whether this wind's tornados are up.</summary>
    public int WindsMade { get; private set; }
    public int WindFlings { get; private set; }
    public SmsGhostWind Wind => _wind != null && IsInstanceValid(_wind) ? _wind : null;
    public bool WindLaunched => _windLaunched;
    /// <summary>The mouth: the tongue's root joint (jnt_sita_1, at the lips) in the world, else a little ahead of and under the body joint.</summary>
    public Vector3 MouthPoint => _skel != null && _bSita >= 0 ? _skel.GlobalTransform * _skel.GetBoneGlobalPose(_bSita).Origin : BodyPoint + new Vector3(MathF.Sin(_yaw), 0f, MathF.Cos(_yaw)) * 2.2f - Vector3.Up * 0.8f;
    public Vector3 LastApproachDir => _lastApproach;
    public Vector3 LastSpot { get; private set; }
    public float HoverOverFloor => _hoverOverFloor;
    public Vector3 ArenaCentre => _arenaCentre;
    public float ArenaRadius => _arenaRadius;
    public SmsTongueStretch Tongue => _tongue;
    public float OpacityShown => _opacityShown;
    /// <summary>jnt_body's place in the world (where the glow sits).</summary>
    public Vector3 BodyPoint => _skel != null && _bBody >= 0 ? _skel.GlobalTransform * _skel.GetBoneGlobalPose(_bBody).Origin : GlobalPosition;
    /// <summary>159: a point (a swung Bomb Boo, with its own reach) is in his body's hit cylinder (HitRadius, HitBelow, HitAbove about jnt_body).</summary>
    public bool InHitCylinder(Vector3 p, float extra = 0f)
    {
        Vector3 b = BodyPoint;
        if (p.Y < b.Y - HitBelow - extra || p.Y > b.Y + HitAbove + extra) return false;
        return new Vector2(p.X - b.X, p.Z - b.Z).Length() <= HitRadius + extra;
    }
    /// <summary>The cylinder's lowest point over the world's floor, for the drives.</summary>
    public float HitBottomY => BodyPoint.Y - HitBelow;

    private const float U = 74.44f;
    // the clips used, their bck frame counts (one bck frame = 1 / 60 s) and loops -- SmsBossTelesa's table
    private const string BckAppear = "btelesa_appear", BckLaugh = "btelesa_laugh", BckWait = "btelesa_wait", BckHit = "btelesa_hit", BckDown = "btelesa_down";
    private static readonly Dictionary<string, (int frames, bool loop)> Clips = new()
    {
        [BckAppear] = (59, false), [BckLaugh] = (89, false), [BckWait] = (60, true), [BckHit] = (42, false), [BckDown] = (199, false),
        ["btelesa_hide"] = (44, false), ["btelesa_roll"] = (139, false), ["btelesa_lick"] = (140, false), ["btelesa_spit"] = (99, false), ["btelesa_bero_hit"] = (59, false), ["btelesa_damage"] = (219, false),
    };
    private static readonly int[] BlinkEnds = { 19, 21, 24, 26, 28, 30, 33, 35, 85, 87, 90, 92, 150 };   // the twait runs' ends (frames)
    private static readonly int[] BlinkEyes = { 0, 1, 2, 1, 0, 1, 2, 1, 0, 1, 2, 1, 0 };                   // open / half / close

    private Node3D _model; private AnimationPlayer _anim; private Skeleton3D _skel; private int _bBody = -1;
    private readonly List<ShaderMaterial> _mats = new(); private ShaderMaterial _bodyMat, _crownMat; private Vector3 _crownRest = new(1f, 1f, 0f), _crownNow = new(1f, 1f, 0f);
    private SmsBoolossus _boolossus; private float _smashT; private bool _smashLaunched, _smashClipEnded; private int _smashLaunchedCount, _bCrown = -1; private float _smashNextAt, _smashBaseAngle; private Vector3 _lastApproach;
    private MeshInstance3D _crownGlow; private StandardMaterial3D _crownGlowMat; private float _crownGlowK, _crownShineK, _pulsePhase; private Color _crownGlowColor = new(1f, 1f, 1f);
    private GpuParticles3D _crownSpark; private ParticleProcessMaterial _crownSparkMat; private float _crownFlashT;
    private SmsGhostLightning _lightning; private float _lightningT; private bool _lightningLaunched, _lightningDone;
    private SmsGhostBreath _breath; private float _breathT, _breathWatchT, _breathYawCentre; private bool _breathLaunched, _breathSwept, _breathClipHeld; private int _breathSide = 1, _bSita = -1;
    private SmsFireballs _fireballs; private float _fireT, _fireWatchT; private bool _fireLaunched, _fireShot, _fireClipHeld;
    private SmsBooVolley _volley; private float _volleyT, _volleyWatchT; private bool _volleyLaunched, _volleySpat, _volleyClipHeld;   // 200
    private int _ambushStage; private float _ambushT, _ambushHuntT, _ambushRingT = -1f, _riseGlowSizeNow = 14f, _riseGlowFlare = 1f; private Vector3 _ambushPool; private bool _ambushHitDone;   // 201
    private SmsBooCircle _circle; private float _circleT, _circleWatchT; private bool _circleLaunched;   // 201
    private SmsGhostWind _wind; private float _windT, _windWatchT; private bool _windLaunched, _windDone;
    /// <summary>Where the crown's sparkles and halo sit: the crown joint lifted CrownFxLift into the crown.</summary>
    public Vector3 CrownFxPoint => CrownPoint + Vector3.Up * CrownFxLift;
    private GpuParticles3D _chika; private Node3D _glow; private SmsActorSound _sound; private SmsTongueStretch _tongue;
    private Mario _mario; private float _yaw, _hoverT, _blinkT; private int _eyeNow = -1; private Vector3 _home; private bool _laughSounded;
    private float _introT, _introFloorY, _introBodyLift; private Vector3 _introTop; private bool _spawnRise, _spawnQuiet;   // 174
    public float IntroBodyLift => _introBodyLift;
    /// <summary>174, for the drives: the intro surface's floor (world y), his top (the hover), how far into the intro phase he is (seconds).</summary>
    public float IntroFloorY => _introFloorY;
    public Vector3 IntroTop => _introTop;
    public float IntroPhaseSeconds => _introT;
    public bool IsSpawnRising => _spawnRise;
    private float _hurtT, _hurtLen, _hurtYaw0; private bool _flashOn;
    // 162
    private readonly Random _rng = new(162);
    private float _gapT, _fadeT, _opacitySolid, _opacityShown, _hoverOverFloor = 4f, _arenaRadius = 12f, _stoneRadius = 12f, _floorAtSpot = float.NaN;
    private Vector3 _arenaCentre, _dest; private bool _moveIsLick, _lickHit, _lickClipEnded, _arenaRead;
    private int _lickStage = -1; private float _lickT, _stageT;

    public override void _Ready()
    {
        _model = GetNodeOrNull<Node3D>("Model");
        if (!IsCopy && !Engine.IsEditorHint()) CallDeferred(nameof(ResetFinalSky));                                // 208b: a fresh fight starts under the boss-arena sky (the sky material is a shared resource)
        if (!IsCopy && !Engine.IsEditorHint() && ShineMadeAhead) CallDeferred(nameof(MakeShineAhead));          // 224: the Shine instanced now (the load's frame), parked hidden till his death
        if (IsCopy)                                                                       // 177: a duplicate carries the real one's RUN-TIME children too -- made again below, so out they go
        {
            foreach (string n in new[] { "CrownGlowFx", "CrownSparkFx" }) { var old = GetNodeOrNull(n); if (old != null) { RemoveChild(old); old.QueueFree(); } }
            if (_model != null) foreach (Node n in _model.FindChildren("TongueStretch", "", true, false)) { n.GetParent().RemoveChild(n); n.QueueFree(); }
        }
        _anim = GetNodeOrNull<AnimationPlayer>("Model/AnimationPlayer");
        if (_model != null)
        {
            if (_anim == null) foreach (Node n in _model.FindChildren("*", "AnimationPlayer", true, false)) { _anim = n as AnimationPlayer; if (_anim != null) break; }
            foreach (Node n in _model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; break; }
            if (_skel != null)
            {
                _bBody = _skel.FindBone("jnt_body"); if (_bBody < 0 && _skel.GetBoneCount() > 1) _bBody = 1;
                _bCrown = _skel.FindBone("jnt_crown");
                _bSita = _skel.FindBone("jnt_sita_1");
                if (!Engine.IsEditorHint()) { _tongue = new SmsTongueStretch { Name = "TongueStretch", Debug = DebugPrint, Stretch = TongueStretch }; _skel.AddChild(_tongue); }
            }
            foreach (Node n in _model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh == null) continue;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    if (mi.GetActiveMaterial(i) is not ShaderMaterial sm) continue;
                    var copy = (ShaderMaterial)sm.Duplicate();
                    mi.SetSurfaceOverrideMaterial(i, copy);
                    _mats.Add(copy);
                    if ((sm.ResourceName ?? "").StartsWith("_mat_body") || _bodyMat == null) _bodyMat = copy;
                    if ((sm.ResourceName ?? "").StartsWith("_mat_crown")) { _crownMat = copy; var c0 = copy.GetShaderParameter("crown_c0"); if (c0.VariantType == Variant.Type.Vector3) _crownRest = _crownNow = c0.AsVector3(); }
                    if ((sm.ResourceName ?? "").StartsWith("_mat_ha")) _teethMat = copy;                                                               // 178: the fangs (the copy's tell)
                }
            }
        }
        else GD.PushWarning($"[SMS kingboo] {GetPath()}: no Model child (btelesa.glb)");
        if (_anim != null) _anim.AnimationFinished += OnClipEnd;
        AddToGroup("sms_rematch_boo");                                                   // 177: every King Boo on the field, the real one and his copies (the Bomb Boos' targets)
        if (IsCopy) RemoveFromGroup("sms_rematch_boss");                                 // 177: the boss group is the REAL one's alone (the supply, the intro, the Bomb Boos' default)
        EyeOpen ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_open.png");
        EyeHalf ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_half.png");
        EyeClose ??= LoadTex("res://models/Eniemes/BossTelesa/N_nbtelesa_eye_close.png");
        _chika = GetNodeOrNull<GpuParticles3D>("ChikaFx"); _glow = GetNodeOrNull<Node3D>("GlowFx");
        foreach (Node3D fx in new Node3D[] { _chika, _glow }) if (fx != null) { fx.TopLevel = true; fx.Visible = false; }
        if (!Engine.IsEditorHint() && CrownGlowSize > 0f)                                 // 165: the crown's light -- a camera-facing additive quad of the port's radial gradient
        {
            var tex = ResourceLoader.Exists("res://assets/Sms/Fx/tex/radgrad_raw.png") ? GD.Load<Texture2D>("res://assets/Sms/Fx/tex/radgrad_raw.png") : null;
            _crownGlowMat = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, NoDepthTest = true,
                AlbedoTexture = tex, AlbedoColor = new Color(1f, 1f, 1f, 0f), RenderPriority = 3
            };
            _crownGlow = new MeshInstance3D { Name = "CrownGlowFx", TopLevel = true, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Mesh = new QuadMesh { Size = Vector2.One * CrownGlowSize }, MaterialOverride = _crownGlowMat };
            AddChild(_crownGlow);
        }
        if (!Engine.IsEditorHint() && CrownSparkles > 0 && _chika != null && _chika.ProcessMaterial is ParticleProcessMaterial chikaMat)   // 165c: the sparkles -- the twinkle stars, smaller, rising off the crown, in the colour
        {
            _crownSpark = (GpuParticles3D)_chika.Duplicate();
            _crownSpark.Name = "CrownSparkFx"; _crownSpark.TopLevel = true; _crownSpark.Visible = false; _crownSpark.Emitting = false; _crownSpark.Amount = CrownSparkles; _crownSpark.Lifetime = 0.55; _crownSpark.OneShot = false;
            _crownSparkMat = (ParticleProcessMaterial)chikaMat.Duplicate();
            _crownSparkMat.EmissionSphereRadius = 1.5f; _crownSparkMat.ScaleMin = 0.4f; _crownSparkMat.ScaleMax = 0.95f; _crownSparkMat.Direction = new Vector3(0f, 1f, 0f); _crownSparkMat.Spread = 70f; _crownSparkMat.InitialVelocityMin = 1.5f; _crownSparkMat.InitialVelocityMax = 4f; _crownSparkMat.Gravity = new Vector3(0f, 2f, 0f);
            _crownSpark.ProcessMaterial = _crownSparkMat;
            AddChild(_crownSpark);
        }
        _opacitySolid = GhostOpacity;
        foreach (var m in _mats) { m.SetShaderParameter("tint", new Vector3(1f, 1f, 1f)); m.SetShaderParameter("mask_k", 0f); }
        SetOpacity(GhostOpacity);
        ApplyEye(0);
        _sound = SmsActorSound.Attach(this);
        if (!IsCopy) { _sfxDbWas = SmsActorSound.MasterDb; _sfxDbSet = true; SmsActorSound.MasterDb = SfxDb; }                                       // 195: the rematch's effects level
        SmashScene ??= ResourceLoader.Exists("res://assets/Sms/Rematch/Boolossus.tscn") ? GD.Load<PackedScene>("res://assets/Sms/Rematch/Boolossus.tscn") : null;
        _yaw = Rotation.Y; _home = Position;
        if (_model != null) _model.Visible = false;
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: {_mats.Count} materials, clips {(_anim != null ? string.Join(",", _anim.GetAnimationList()) : "none")}, body joint {_bBody}, tongue {(_tongue != null && _tongue.Bound ? "bound" : "none")}, crown material {(_crownMat != null ? "found" : "NONE")}, crown joint {_bCrown}, Boolossus scene {(SmashScene != null ? "loaded" : "NONE")}");
        SmsKingBooDriveHook();
    }
    partial void SmsKingBooDriveHook();
    private static Texture2D LoadTex(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

    /// <summary>174: the fight's start after the intro's surface -- he rises out of the floor at his spot (the appear clip and voice, over SpawnRiseSeconds)
    /// straight into the wait: no laugh (the intro had it).</summary>
    public void SpawnRising()
    {
        if (State != Phase.Hidden || Engine.IsEditorHint() || CopyMayNot("spawn")) return;
        _spawnRise = true; _spawnQuiet = true;
        Spawn();
    }
    /// <summary>He appears: the appear clip, then the laugh with the long laugh, then the wait. Nothing happens twice.</summary>
    public void Spawn()
    {
        if (State != Phase.Hidden || Engine.IsEditorHint() || CopyMayNot("spawn")) return;
        State = Phase.Appear; _home = Position; _hoverT = 0f;   // the hover bobs about where he was put
        Hp = Math.Max(1, HitPoints); HitsTaken = 0;
        if (_model != null) _model.Visible = true;
        if (_tongue != null) { _tongue.Stretch = TongueStretch; _tongue.On = TongueStretch > 1.001f; }
        SetCrownGlow(_crownGlowColor, 0f);
        PlayClip(BckAppear);
        ReadArena();
        if (HoverLift != 0f) { Position += Vector3.Up * HoverLift; _home = Position; _hoverOverFloor += HoverLift; }   // 173: he sits higher than placed
        if (_spawnRise)                                                                                                  // 174: up out of the floor
        {
            _introTop = Position; _introFloorY = Position.Y - _hoverOverFloor; _introT = 0f;
            Position = new Vector3(Position.X, _introFloorY - IntroSinkDepth, Position.Z);
            _riseClipPending = RiseFx && RiseTellSeconds > 0f; _riseClipStarted = false; RiseLeapTopY = float.NegativeInfinity;   // 181: the tell first (the appear clip played above is replaced till then)
            if (_riseClipPending) { _introT = -RiseTellSeconds; PlayClip(BckWait); }
            else StartAppearClip();
            RiseFxBegin(new Vector3(_introTop.X, _introFloorY, _introTop.Z));                                            // 181: the glow on the stone, the wisps
        }
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: appears at {GlobalPosition} ({_hoverOverFloor:F2} m over the floor{(HoverLift != 0f ? $", lifted {HoverLift:F1}" : "")}{(_spawnRise ? $", rising out of the floor over {SpawnRiseSeconds:F1} s" : "")}; the arena's middle {_arenaCentre}, radius {_arenaRadius:F1})");
        MusicCue(BossMusicCues.Fight, "the fight begins");                                                                                         // 195 / 197: the fight's cue (the last chance when the cutscene's never came)
        EmitSignal(SignalName.Spawned);
    }

    /// <summary>174: the intro's SURFACE at a spot (placedAt: the hover height as the intro places him, the lift added here), facing yaw -- the rise
    /// out of the floor, the laugh, the sink; IntroSurfacing now, IntroSunk at the end (he is Hidden again: SpawnRising starts the fight).</summary>
    public void IntroSurface(Vector3 placedAt, float yaw)
    {
        if (State != Phase.Hidden || Engine.IsEditorHint() || CopyMayNot("surface for the intro")) return;
        GlobalPosition = placedAt; _yaw = yaw; Rotation = new Vector3(0f, yaw, 0f);
        Hp = Math.Max(1, HitPoints); HitsTaken = 0;
        ReadArena();
        if (HoverLift != 0f) { Position += Vector3.Up * HoverLift; _hoverOverFloor += HoverLift; }
        _introTop = Position; _introFloorY = Position.Y - _hoverOverFloor; _introT = 0f; _hoverT = 0f; _introBodyLift = 0f;
        Position = new Vector3(Position.X, _introFloorY - IntroSinkDepth, Position.Z);
        if (_model != null) _model.Visible = true;
        if (_tongue != null) { _tongue.Stretch = TongueStretch; _tongue.On = TongueStretch > 1.001f; }
        SetCrownGlow(_crownGlowColor, 0f);
        State = Phase.IntroRise;
        MusicCue(BossMusicCues.StanceCut, "the intro's cut to the stance");                                                                        // 196 / 197
        _riseClipPending = RiseFx && RiseTellSeconds > 0f; _riseClipStarted = false; RiseLeapTopY = float.NegativeInfinity;   // 181: the tell first -- the glow and the wisps alone, then the clip and the rise
        if (_riseClipPending) { _introT = -RiseTellSeconds; PlayClip(BckWait); }
        else StartAppearClip();
        RiseFxBegin(new Vector3(_introTop.X, _introFloorY, _introTop.Z));                                                // 181: the glow on the stone, the wisps
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: INTRO -- surfaces at {_introTop} ({_hoverOverFloor:F2} m over the floor at {_introFloorY:F2}) from {IntroSinkDepth} m under, over {IntroRiseSeconds:F1} s; laughs {IntroLaughSeconds:F1} s; sinks over {IntroSinkSeconds:F1} s");
        EmitSignal(SignalName.IntroSurfacing);
    }
    /// <summary>174: the intro cut short (the player skipped): under the floor at once, hidden; IntroSunk.</summary>
    public void IntroAbort()
    {
        if (State != Phase.IntroRise && State != Phase.IntroLaugh && State != Phase.IntroSink) return;
        Position = new Vector3(Position.X, _introFloorY - IntroSinkDepth, Position.Z);
        if (_model != null) _model.Visible = false;
        State = Phase.Hidden; _glowBoost = 1f; RiseFxEnd(); _riseClipPending = false;
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: INTRO cut short -- under the floor");
        EmitSignal(SignalName.IntroSunk);
    }
    /// <summary>174: a voice from his sound node for the intro (the laugh before he shows).</summary>
    public void PlayVoice(string se, float db = 0f) { if (_sound == null) _sound = SmsActorSound.Attach(this); if (_sound != null && !string.IsNullOrEmpty(se)) _sound.Play(se, db); }
    private void IntroTick(float dt)
    {
        _introT += dt;
        if (State == Phase.IntroRise)
        {
            RiseClipWhenDue();
            if (_riseClipStarted) RiseNodeTick();                                                                                       // 182: the clip's own leap is the rise
            else if (!_riseClipPending)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_introT / Mathf.Max(0.1f, IntroRiseSeconds), 0f, 1f));                  // 174's lerp: no appear clip
                Position = new Vector3(_introTop.X, Mathf.Lerp(_introFloorY - IntroSinkDepth, _introTop.Y, k), _introTop.Z);
            }
            FaceTick(dt);
            RiseFxTick(dt);
            if (_introT >= RiseLength)
            {
                RiseFxEnd();
                State = Phase.IntroLaugh; _introT = 0f; _hoverT = 0f; LaughPlayed = true;
                PlayClip(BckLaugh);
                string laugh = IntroLaughSeNow;                                                                                                  // 186: the casino intro's chuckle, pitched
                if (_sound != null && !string.IsNullOrEmpty(laugh)) _sound.Play(laugh, LaughDb, IntroLaughPitchNow);                             // 192: from the ramp's low start
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: INTRO -- up; the laugh {laugh} at pitch {IntroLaughPitchNow:F2} rising to {IntroLaughPitch * IntroLaughPitchTo:F2} ({IntroLaughLength:F1} s{(float.IsNaN(IntroLaughOverride) ? "" : $", trimmed from {IntroLaughSeconds:F1} for the music's cue")})");
                EmitSignal(SignalName.Laughed);
            }
        }
        else if (State == Phase.IntroLaugh)
        {
            _hoverT += dt;
            if (_sound != null && (IntroLaughPitchFrom != 1f || IntroLaughPitchTo != 1f) && !string.IsNullOrEmpty(IntroLaughSeNow)) _sound.SetPitch(IntroLaughSeNow, IntroLaughPitchNow);   // 192: the chuckle climbs
            if (IntroLaughBodyHeld) _introBodyLift = Mathf.Lerp(_introBodyLift, BodyPoint.Y - GlobalPosition.Y, 1f - MathF.Exp(-dt / Mathf.Max(0.05f, IntroLaughHoldSeconds)));   // the clip's lift of the body, taken out
            Position = _introTop + Vector3.Up * ((HoverBob > 0f && HoverBobSeconds > 0f ? MathF.Sin(_hoverT / HoverBobSeconds * MathF.Tau) * HoverBob : 0f) - _introBodyLift);
            FaceTick(dt);
            if (_introT >= IntroLaughLength)                                                                                                 // 197: the director's trim, else the knob
            {
                State = Phase.IntroSink; _introT = 0f; _introTop = new Vector3(_introTop.X, Position.Y, _introTop.Z); _introBodyLift = 0f;
                if (IntroLaughStopsAtDive && _sound != null && !string.IsNullOrEmpty(IntroLaughSeNow)) _sound.Stop(IntroLaughSeNow);           // 186: the chuckle cut at the dive
                _diveOn = !string.IsNullOrEmpty(IntroDiveClip) && _anim != null && _anim.HasAnimation(IntroDiveClip);                       // 180: the hide clip's dive
                if (_diveOn)
                {
                    PlayClip(IntroDiveClip); _anim.SpeedScale *= Mathf.Max(0.1f, IntroDiveClipSpeed);
                    _diveLen = (float)(_anim.GetAnimation(IntroDiveClip).Length / Mathf.Max(0.01f, _anim.SpeedScale));
                    _diveSparkled = _diveSplashed = false; _diveBodyTop = float.NegativeInfinity; _diveLastBodyY = BodyPoint.Y; DiveSplashT = -1f;
                    if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: INTRO -- the DIVE: {IntroDiveClip} over {_diveLen:F2} s (the body at {BodyPoint.Y:F2}, the floor {_introFloorY:F2})");
                }
                else
                {
                    PlayClip(BckWait);
                    if (_sound != null && !string.IsNullOrEmpty(VanishSe)) _sound.Play(VanishSe);
                    if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: INTRO -- sinks");
                }
            }
        }
        else if (State == Phase.IntroSink)
        {
            if (_diveOn) DiveTick();
            else
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_introT / Mathf.Max(0.1f, IntroSinkSeconds), 0f, 1f));
                Position = new Vector3(_introTop.X, Mathf.Lerp(_introTop.Y, _introFloorY - IntroSinkDepth, k), _introTop.Z);
            }
            if (_introT >= (_diveOn ? _diveLen : IntroSinkSeconds))
            {
                if (_model != null) _model.Visible = false;
                State = Phase.Hidden; _glowBoost = 1f;
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: INTRO -- under the floor, hidden; the fight may be started (SpawnRising){(_diveOn ? $" -- the dive: the body topped at {_diveBodyTop:F2}, the splash at {DiveSplashT:F2} s" : "")}");
                EmitSignal(SignalName.IntroSunk);
            }
        }
    }
    private bool _diveOn, _diveSparkled, _diveSplashed; private float _diveLen, _diveBodyTop = float.NegativeInfinity, _diveLastBodyY, _glowBoost = 1f;
    private sealed class Flash { public MeshInstance3D Mi; public StandardMaterial3D Mat; public float T, Size, Seconds, From, To; public Color? Col; }   // 183: Col = its own colour (the Shine's gold), else DiveColor
    private readonly List<Flash> _flashes = new();                                                            // 181: the flashes alive (the dive's, a rise's, the teleports')
    private MeshInstance3D _diveRing; private StandardMaterial3D _diveRingMat; private float _diveFxT;
    private bool _riseFxOn, _riseBroke; private float _riseT, _riseFloorY, _riseBreakT = -1f, _riseGlowT, _riseGlowBreakT = -1f; private Vector3 _riseSpot; private MeshInstance3D _riseGlow; private StandardMaterial3D _riseGlowMat; private GpuParticles3D _riseWisps, _riseStars;
    /// <summary>180, for the drives: the dive (on, the twinkle burst fired, the splash fired and when, how high the body leapt, a flash alive); 181: the rise
    /// (its effects running, the floor glow up, the body broken through and when, the stars streaming), the teleports' effects fired.</summary>
    public bool DiveOn => _diveOn;
    public bool DiveSparkled => _diveSparkled;
    public bool DiveSplashed => _diveSplashed;
    public float DiveSplashT { get; private set; } = -1f;
    public float DiveBodyTopY => _diveBodyTop;
    public bool DiveFlashUp => _flashes.Count > 0;
    public bool RiseFxOn => _riseFxOn;
    public bool RiseGlowUp => _riseGlow != null && IsInstanceValid(_riseGlow) && _riseGlow.Visible;
    public bool RiseBroke => _riseBroke;
    public float RiseBreakT => _riseBreakT;
    public bool RiseStarsOn => _riseStars != null && IsInstanceValid(_riseStars) && _riseStars.Emitting;
    public int TeleportOutFxFired { get; private set; }
    public int TeleportInFxFired { get; private set; }
    /// <summary>180: the intro's DIVE -- the node holds at the laugh height while the hide clip leaps the body up and plunges it through the floor; the twinkle burst
    /// at the top of the leap (the body starts down, or 0.3 of the clip in), the glow swelling through the leap and out once the body is under, the SPLASH as the body
    /// comes down to DiveSplashAt over the floor.</summary>
    private void DiveTick()
    {
        Position = _introTop;
        float by = BodyPoint.Y;
        if (by > _diveBodyTop) _diveBodyTop = by;
        float leap = Mathf.Clamp(_introT / Mathf.Max(0.05f, 0.25f * _diveLen), 0f, 1f);
        _glowBoost = by > _introFloorY + 1f ? 1f + (DiveGlowBoost - 1f) * leap : 0f;
        if (!_diveSparkled && _introT > 0.08f && (by < _diveLastBodyY - 0.01f || _introT >= 0.3f * _diveLen)) { _diveSparkled = true; DiveSparkle(BodyPoint); }
        if (!_diveSplashed && by <= _introFloorY + DiveSplashAt) { _diveSplashed = true; DiveSplashT = _introT; DiveSplash(new Vector3(BodyPoint.X, _introFloorY, BodyPoint.Z)); }
        _diveLastBodyY = by;
    }
    /// <summary>The twinkle stars burst off him at the top of the leap: his ChikaFx emitter copied as a one-shot, flung every way under a light gravity.</summary>
    private void DiveSparkle(Vector3 at) { StarBurst("DiveSparkFx", at, DiveSparkles, 1f); if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the DIVE's twinkle burst at {at} ({_introT:F2} s in)"); }
    /// <summary>A one-shot burst of his twinkle stars (his ChikaFx emitter copied): count, at a point, the dive's sizes x scale (he is 6 m across: big stars).</summary>
    private GpuParticles3D StarBurst(string name, Vector3 at, int count, float scale, Color? col = null)
    {
        if (count <= 0 || _chika == null || _chika.ProcessMaterial is not ParticleProcessMaterial chikaMat) return null;
        var p = (GpuParticles3D)_chika.Duplicate();
        p.Name = name; p.TopLevel = true; p.Visible = true; p.Amount = count; p.Lifetime = 0.9; p.OneShot = true; p.Explosiveness = 1f; p.Emitting = false; p.LocalCoords = false;
        var pm = (ParticleProcessMaterial)chikaMat.Duplicate();
        pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere; pm.EmissionSphereRadius = 2.2f * scale; pm.Direction = Vector3.Up; pm.Spread = 180f;
        pm.InitialVelocityMin = 4f * scale; pm.InitialVelocityMax = 10f * scale; pm.Gravity = new Vector3(0f, -4f, 0f); pm.ScaleMin = 1.6f * scale; pm.ScaleMax = 3f * scale;
        if (col.HasValue) pm.Color = new Color(col.Value.R, col.Value.G, col.Value.B, 1f);                                                   // 183: the Shine's gold
        p.ProcessMaterial = pm;
        AddChild(p); p.GlobalPosition = at; p.Finished += p.QueueFree; p.Emitting = true;
        return p;
    }
    /// <summary>A flash of light: a flat additive radial on the stone (flat) or a camera-facing one at a point, size m, growing from -> to of it and fading over
    /// seconds (FxTick). Drawn over the depth: the arena's floor is a shallow dome -- a flat quad this wide dipped under it and was cut in chunks.</summary>
    private void FlashAt(string name, Vector3 at, float size, float seconds, bool flat, float from = 0.5f, float to = 1.1f, Color? col = null)
    {
        var tex = FxTex("radgrad_raw"); if (size <= 0f || tex == null) return;
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, NoDepthTest = true, AlbedoTexture = tex, AlbedoColor = col.HasValue ? Bright(col.Value, 0.95f) : Bright(0.95f), RenderPriority = 4, BillboardMode = flat ? BaseMaterial3D.BillboardModeEnum.Disabled : BaseMaterial3D.BillboardModeEnum.Enabled };
        var mi = new MeshInstance3D { Name = name, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Mesh = new QuadMesh { Size = Vector2.One, Orientation = flat ? PlaneMesh.OrientationEnum.Y : PlaneMesh.OrientationEnum.Z, Material = mat }, Scale = Vector3.One * size * from };
        AddChild(mi); mi.GlobalPosition = at + (flat ? Vector3.Up * 0.06f : Vector3.Zero);
        _flashes.Add(new Flash { Mi = mi, Mat = mat, T = 0f, Size = size, Seconds = Mathf.Max(0.01f, seconds), From = from, To = to, Col = col });
    }
    /// <summary>The shock ring racing out along the floor from a point to `size` x 2.2 over seconds x 1.8, fading (FxTick); one at a time.</summary>
    private void ShockRingAt(Vector3 at, float size, float seconds)
    {
        var ringTex = FxTex("hamonring2_add") ?? FxTex("hamonring2"); if (ringTex == null) return;
        if (_diveRing != null && IsInstanceValid(_diveRing)) { _diveRing.QueueFree(); _diveRing = null; }
        _diveRingMat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, NoDepthTest = true, AlbedoTexture = ringTex, AlbedoColor = Bright(0.9f), RenderPriority = 4 };
        _diveRing = new MeshInstance3D { Name = "ShockRingFx", TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Mesh = new QuadMesh { Size = Vector2.One, Orientation = PlaneMesh.OrientationEnum.Y, Material = _diveRingMat }, Scale = Vector3.One * 2f };
        AddChild(_diveRing); _diveRing.GlobalPosition = at + Vector3.Up * 0.1f;
        _diveFxT = 0f; _ringSize = size; _ringSeconds = Mathf.Max(0.01f, seconds);
    }
    private float _ringSize = 12f, _ringSeconds = 0.45f;
    /// <summary>The floor BURST where his body meets the stone (the dive's splash; 181: the rise's breakthrough too): the flash, the shock ring, a ring of ghost mist
    /// racing out along the floor, a splash of it thrown up.</summary>
    private void FloorBurst(string name, Vector3 at)
    {
        FlashAt(name + "Flash", at, DiveFlashSize, DiveFlashSeconds, flat: true);
        if (DiveShockRing) ShockRingAt(at, DiveFlashSize, DiveFlashSeconds);
        Color mist = MistColor;
        if (DiveRingPuffs > 0) DiveBurst(name + "RingPuffs", DiveRingPuffs, at + Vector3.Up * 0.5f, Vector3.Up * 0.05f, 180f, 8f, 15f, 2.5f, 6f, mist, 0.75f, 1.0, 4f, 0.3f, flat: true);   // a 6 m ghost: the mist races out wide
        if (DiveSplashPuffs > 0) DiveBurst(name + "SplashPuffs", DiveSplashPuffs, at + Vector3.Up * 0.8f, Vector3.Up, 35f, 7f, 15f, 2f, 5f, mist, 0.75f, 1.1, 2.5f, -6f);
    }
    private Color MistColor => new Color(Mathf.Lerp(DiveColor.R, 1f, 0.3f), Mathf.Lerp(DiveColor.G, 1f, 0.3f), Mathf.Lerp(DiveColor.B, 1f, 0.3f));
    /// <summary>The body goes through the floor: the floor burst, the camera kicked, the vanish voice.</summary>
    private void DiveSplash(Vector3 at)
    {
        FloorBurst("Dive", at);
        if (DiveShake > 0f) SmsCameraShake.Current?.Start(SmsCameraShake.Kind.Damage, DiveShake);
        if (_sound != null && !string.IsNullOrEmpty(VanishSe)) _sound.Play(VanishSe);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the DIVE's splash at {at} ({_introT:F2} s in; the body at {BodyPoint.Y:F2})");
    }
    /// <summary>The flashes and the shock ring run out: a flash grows from -> to of its size and fades over its seconds; the ring races out and fades. 181: the rise's
    /// floor glow pulses and grows while it is up.</summary>
    private void FxTick(float dt)
    {
        for (int i = _flashes.Count - 1; i >= 0; i--)
        {
            var fl = _flashes[i];
            if (fl.Mi == null || !IsInstanceValid(fl.Mi)) { _flashes.RemoveAt(i); continue; }
            fl.T += dt; float f = Mathf.Clamp(fl.T / fl.Seconds, 0f, 1f);
            fl.Mi.Scale = Vector3.One * fl.Size * Mathf.Lerp(fl.From, fl.To, f);
            fl.Mat.AlbedoColor = fl.Col.HasValue ? Bright(fl.Col.Value, (1f - f) * (1f - f) * 0.95f) : Bright((1f - f) * (1f - f) * 0.95f);
            if (f >= 1f) { fl.Mi.QueueFree(); _flashes.RemoveAt(i); }
        }
        if (_diveRing != null && IsInstanceValid(_diveRing))
        {
            _diveFxT += dt;
            float r = Mathf.Clamp(_diveFxT / (_ringSeconds * 1.8f), 0f, 1f);
            _diveRing.Scale = Vector3.One * Mathf.Lerp(2f, _ringSize * 2.2f, Mathf.SmoothStep(0f, 1f, r));
            _diveRingMat.AlbedoColor = Bright((1f - r) * 0.9f);
            if (r >= 1f) { _diveRing.QueueFree(); _diveRing = null; }
        }
        if (_riseGlow != null && IsInstanceValid(_riseGlow) && _riseGlow.Visible)
        {
            _riseGlowT += dt;                                                                                                                   // its own clock: it fades on after the rise is over
            float a = _riseGlowBreakT >= 0f ? Mathf.Max(0f, 1f - (_riseGlowT - _riseGlowBreakT) / 0.4f) : Mathf.Clamp(_riseGlowT / 0.3f, 0f, 1f);   // in over 0.3 s, out over 0.4 s after the break
            float pulse = 0.75f + 0.25f * MathF.Sin(MathF.Tau * RiseGlowHz * _riseGlowT);
            _riseGlow.Scale = Vector3.One * _riseGlowSizeNow * _riseGlowFlare * (0.7f + 0.3f * Mathf.Clamp(_riseGlowT / 1.2f, 0f, 1f)) * (0.92f + 0.08f * pulse);   // 201: the ambush's pool is smaller (AmbushPoolSize) and flares at its tell
            _riseGlowMat.AlbedoColor = Bright(Mathf.Min(1f, a * pulse * _riseGlowFlare));
            if (_riseGlowBreakT >= 0f && a <= 0f) _riseGlow.Visible = false;
        }
    }
    private bool AnyFxAlive => _flashes.Count > 0 || _diveRing != null || (_riseGlow != null && IsInstanceValid(_riseGlow) && _riseGlow.Visible);
    /// <summary>DiveColor x FlashBoost (over 1: an additive quad brighter than its texture) at an alpha.</summary>
    private Color Bright(float alpha) => new Color(DiveColor.R * FlashBoost, DiveColor.G * FlashBoost, DiveColor.B * FlashBoost, alpha);
    private Color Bright(Color c, float alpha) => new Color(c.R * FlashBoost, c.G * FlashBoost, c.B * FlashBoost, alpha);
    private bool _riseClipPending, _riseClipStarted; private float _riseLen;
    /// <summary>182, for the drives: the highest the body got in the rise's leap; the rise's length (the clip's at its speed).</summary>
    public float RiseLeapTopY { get; private set; } = float.NegativeInfinity;
    public float RiseLength => _riseClipStarted ? _riseLen : IntroRiseSeconds;
    /// <summary>181: the tell is over (the rise clock has reached 0): the appear clip and the appear voice start -- the body comes up.</summary>
    private void RiseClipWhenDue()
    {
        if (!_riseClipPending || _introT < 0f) return;
        StartAppearClip();
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the RISE's tell is over -- the appear clip, he comes up");
    }
    /// <summary>182: the appear clip at IntroAppearClipSpeed, the node put at the hover height (the clip's own leap carries the body from 17 m under the node up
    /// and back to it), the appear voice; the rise lasts the clip. No clip: 174's lerp (IntroRiseSeconds / SpawnRiseSeconds).</summary>
    private void StartAppearClip()
    {
        _riseClipPending = false;
        PlayClip(BckAppear);
        if (_anim != null && _anim.HasAnimation(BckAppear))
        {
            _anim.SpeedScale *= Mathf.Max(0.1f, IntroAppearClipSpeed);
            _riseLen = (float)(_anim.GetAnimation(BckAppear).Length / Mathf.Max(0.01f, _anim.SpeedScale));
            _riseClipStarted = true;
            Position = _introTop;
        }
        else _riseClipStarted = false;
        if (_sound != null && !string.IsNullOrEmpty(ArriveSe)) _sound.Play(ArriveSe);
    }
    /// <summary>182: the node through the rise -- at the hover height, dropped by RiseLeapDamp of the clip's lift of the body over it, so the leap's overshoot is
    /// damped and the body lands at the hover as the clip settles (the lift read off the pose: the body joint over the node).</summary>
    private void RiseNodeTick()
    {
        float lift = BodyPoint.Y - GlobalPosition.Y;
        Position = new Vector3(_introTop.X, _introTop.Y - RiseLeapDamp * Mathf.Max(0f, lift), _introTop.Z);
        if (BodyPoint.Y > RiseLeapTopY) RiseLeapTopY = BodyPoint.Y;
    }
    /// <summary>181: a rise begins (the intro's surface, the fight's spawn): the glow on the stone where he will come up and the wisps seeping out of it.</summary>
    private void RiseFxBegin(Vector3 floorSpot)
    {
        if (!RiseFx) return;
        _riseFxOn = true; _riseBroke = false; _riseT = 0f; _riseBreakT = -1f; _riseGlowT = 0f; _riseGlowBreakT = -1f; _riseSpot = floorSpot; _riseFloorY = floorSpot.Y; _riseGlowSizeNow = RiseGlowSize; _riseGlowFlare = 1f;
        var tex = FxTex("radgrad_raw");
        if (RiseGlowSize > 0f && tex != null)
        {
            if (_riseGlow == null || !IsInstanceValid(_riseGlow))
            {
                _riseGlowMat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, NoDepthTest = true, AlbedoTexture = tex, AlbedoColor = new Color(DiveColor.R, DiveColor.G, DiveColor.B, 0f), RenderPriority = 3 };
                _riseGlow = new MeshInstance3D { Name = "RiseGlowFx", TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Mesh = new QuadMesh { Size = Vector2.One, Orientation = PlaneMesh.OrientationEnum.Y, Material = _riseGlowMat } };
                AddChild(_riseGlow);
            }
            _riseGlow.GlobalPosition = floorSpot + Vector3.Up * 0.05f; _riseGlow.Scale = Vector3.One * RiseGlowSize * 0.7f; _riseGlow.Visible = true;
            _riseGlowMat.AlbedoColor = Bright(0f);
        }
        if (RiseWisps > 0)
        {
            if (_riseWisps != null && IsInstanceValid(_riseWisps)) { _riseWisps.QueueFree(); _riseWisps = null; }
            _riseWisps = Emitter("RiseWispsFx", RiseWisps, 1.1, floorSpot + Vector3.Up * 0.3f, Vector3.Up, 70f, 1.5f, 3.2f, 2f, 4.5f, MistColor, 0.6f, 1.5f, 0.4f, 2.2f);
        }
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the RISE's glow and wisps at {floorSpot}");
    }
    /// <summary>181: a rise's tick -- the body breaking the floor fires the floor burst, the kick, the glow's swell and the stars streaming up round him.</summary>
    private void RiseFxTick(float dt)
    {
        if (!_riseFxOn) return;
        _riseT += dt;
        if (!_riseBroke && BodyPoint.Y > _riseFloorY - RiseBreakAt)
        {
            _riseBroke = true; _riseBreakT = _riseT; _riseGlowBreakT = _riseGlowT;
            Vector3 at = new Vector3(BodyPoint.X, _riseFloorY, BodyPoint.Z);
            FloorBurst("Rise", at);
            if (RiseShake > 0f) SmsCameraShake.Current?.Start(SmsCameraShake.Kind.Damage, RiseShake);
            if (_riseWisps != null && IsInstanceValid(_riseWisps)) _riseWisps.Emitting = false;
            if (RiseSparkles > 0 && _chika != null && _chika.ProcessMaterial is ParticleProcessMaterial chikaMat)
            {
                if (_riseStars != null && IsInstanceValid(_riseStars)) { _riseStars.QueueFree(); _riseStars = null; }
                _riseStars = (GpuParticles3D)_chika.Duplicate();
                _riseStars.Name = "RiseStarsFx"; _riseStars.TopLevel = true; _riseStars.Visible = true; _riseStars.Amount = RiseSparkles; _riseStars.Lifetime = 1.0; _riseStars.OneShot = false; _riseStars.Emitting = false; _riseStars.LocalCoords = false;
                var pm = (ParticleProcessMaterial)chikaMat.Duplicate();
                // 182: sparks shed OUT and DOWN off his body as he rises (Jaysonn: "not a fan of the crown sparkle effect" -- 181's column of stars rose up past his
                // crown, and stars sent up off the body gathered at the crown as he landed): flung sideways and down out of the sphere about his body, falling
                pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere; pm.EmissionSphereRadius = 3.5f; pm.Direction = Vector3.Down; pm.Spread = 100f;
                pm.InitialVelocityMin = 2f; pm.InitialVelocityMax = 6f; pm.Gravity = new Vector3(0f, -8f, 0f); pm.ScaleMin = 1.2f; pm.ScaleMax = 2.2f;
                _riseStars.ProcessMaterial = pm; _riseStars.Lifetime = 0.8;
                AddChild(_riseStars); _riseStars.GlobalPosition = BodyPoint + Vector3.Up * 1.5f; _riseStars.Emitting = true;                  // the body's middle (the joint sits low in the sphere)
            }
            if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the RISE breaks the floor at {at} ({_riseT:F2} s in; the body at {BodyPoint.Y:F2})");
        }
        if (_riseBroke)
        {
            _glowBoost = 1f + (RiseGlowBoost - 1f) * Mathf.Max(0f, 1f - (_riseT - _riseBreakT) / 0.6f);
            if (_riseStars != null && IsInstanceValid(_riseStars))
            {
                _riseStars.GlobalPosition = BodyPoint + Vector3.Up * 1.5f;
                // 182: the stars stream off him only while he shoots UP to his hover height -- stars shed over it (the leap's 4 m overshoot and the drop back,
                // half a second) sank slower than he did, and he left them hanging about his crown as he landed
                if (_riseStars.Emitting && (BodyPoint.Y > _introTop.Y || BodyPoint.Y < RiseLeapTopY - 0.3f)) _riseStars.Emitting = false;
            }
        }
    }
    /// <summary>181: the rise is over (he is up, or the intro was cut): the stars stop streaming (the live ones finish), the wisps too, the glow on the stone goes out.</summary>
    private void RiseFxEnd()
    {
        if (!_riseFxOn) return;
        _riseFxOn = false; _glowBoost = 1f;
        if (_riseStars != null && IsInstanceValid(_riseStars)) { var s = _riseStars; s.Emitting = false; GetTree().CreateTimer(1.2).Timeout += () => { if (IsInstanceValid(s)) s.QueueFree(); }; _riseStars = null; }
        if (_riseWisps != null && IsInstanceValid(_riseWisps)) { var w = _riseWisps; w.Emitting = false; GetTree().CreateTimer(1.8).Timeout += () => { if (IsInstanceValid(w)) w.QueueFree(); }; _riseWisps = null; }
        if (_riseGlow != null && IsInstanceValid(_riseGlow) && !_riseBroke) _riseGlow.Visible = false;
    }
    /// <summary>181: the teleports' effects -- a poof of ghost mist and a burst of stars at his body the moment he is gone.</summary>
    private void TeleportOutFx()
    {
        if (!TeleportFx) return;
        Vector3 at = BodyPoint + Vector3.Up * 1.5f; float s = TeleportFxScale;                                        // the body joint sits low in the sphere
        if (TeleportPuffs > 0) DiveBurst("TeleportOutPuffs", TeleportPuffs, at, Vector3.Up, 180f, 3f * s, 6f * s, 2f * s, 4.5f * s, MistColor, 0.8f, 0.8, 3f, 0.5f);
        StarBurst("TeleportOutStars", at, TeleportSparkles, s);
        TeleportOutFxFired++;
    }
    /// <summary>181: a flash and a poof where he fades in (the arrival; a copy's too) -- at his body's middle. 182 (Jaysonn: "not a fan of the crown sparkle effect"):
    /// no stars here, and the flash down off the crown; TeleportInStars puts the stars back.</summary>
    private void TeleportInFx()
    {
        if (!TeleportFx) return;
        Vector3 at = Position + Vector3.Up * 2f; float s = TeleportFxScale;
        FlashAt("TeleportInFlash", at, DiveFlashSize * 0.8f * s, DiveFlashSeconds, flat: false, 0.5f, 1.2f);
        if (TeleportPuffs > 0) DiveBurst("TeleportInPuffs", TeleportPuffs, at, Vector3.Up, 180f, 3f * s, 6f * s, 2f * s, 4.5f * s, MistColor, 0.8f, 0.8, 3f, 0.5f);
        if (TeleportInStars) StarBurst("TeleportInStars", at, TeleportSparkles, s);
        TeleportInFxFired++;
    }

    // ---- 183: THE END -- the defeat and the Shine ---------------------------------------------------------------------------------------------
    private bool _deathOn, _shinePending; private float _deathT, _deathLen, _deathRumbleT, _shineT; private GpuParticles3D _deathMist, _deathStars; private Vector3 _burstAt, _deathHome; private Node3D _shine;
    /// <summary>183, for the drives: the death effects running (through the down clip); he burst (where); the Shine node once it is in the arena.</summary>
    public bool DeathFxOn => _deathOn;
    public bool Burst { get; private set; }
    public Vector3 BurstAt => _burstAt;
    public Node3D Shine => _shine != null && IsInstanceValid(_shine) ? _shine : null;
    public bool ShineSpawned { get; private set; }
    /// <summary>221, for the drives: the death camera's phase (0 off, 1 the pan out, 2 the hold, 3 the glide back), its node (null when off), the camera it took
    /// the picture from and hands it back to, the wide shot's spot (after the fit), its spot as the knobs put it, how far the fit pulled it back, its look point.</summary>
    public int DeathCamPhase => _deathCamPhase;
    public Camera3D DeathCamNode => _deathCam != null && IsInstanceValid(_deathCam) ? _deathCam : null;
    public Camera3D DeathCamReturnTo => _deathCamWas != null && IsInstanceValid(_deathCamWas) ? _deathCamWas : null;
    public Vector3 DeathCamWidePos => _dcWidePos;
    public Vector3 DeathCamWideBase => _dcWideBase;
    public float DeathCamPushedBack => _dcPushedBack;
    public Vector3 DeathCamWideLook => _dcWideLook;
    public bool HurtFlashOn => _flashOn;
    public float GlowBoostNow => _glowBoost;
    public float OpacitySolidNow => _opacitySolid;
    // ---- 195: the boss music and the mix ------------------------------------------------------------------------------------------------------------------
    private float _sfxDbWas; private bool _sfxDbSet;
    /// <summary>195, for the drives: the music started (and is playing now); faded out.</summary>
    public bool BossMusicStarted { get; private set; }
    public bool BossMusicPlaying => BossMusic != null && IsInstanceValid(BossMusic) && BossMusic.Playing;
    public bool BossMusicFaded { get; private set; }
    public float BossMusicVolumeDb => BossMusic != null && IsInstanceValid(BossMusic) ? BossMusic.VolumeDb : float.NaN;
    /// <summary>197: a cue of the intro or the fight reached -- the music starts when it is the cue set (the fight: the last chance when none came).</summary>
    public void MusicCue(BossMusicCues cue, string why)
    {
        if (cue == BossMusicCue || (cue == BossMusicCues.Fight && !BossMusicStarted)) StartBossMusic(why);
    }
    /// <summary>197, for the drives: what plays -- the file's name, "stream" for a hand-set one, "casino" for the filled theme; its length.</summary>
    public string BossMusicStreamName { get; private set; } = "";
    public float BossMusicLength => BossMusic != null && IsInstanceValid(BossMusic) && BossMusic.Stream != null ? (float)BossMusic.Stream.GetLength() : 0f;
    /// <summary>The boss music starts (once; the real one's alone): the stream set, the file, or the casino theme; looped; at its level plus BossMusicDb.</summary>
    private void StartBossMusic(string why)
    {
        if (IsCopy || !BossMusicOn || BossMusicStarted) return;
        if (BossMusic == null || !IsInstanceValid(BossMusic)) { BossMusic = new AudioStreamPlayer { Name = "Music_BossMusic", Bus = SmsActorSound.MusicBus }; AddChild(BossMusic); }
        ReadMusicBase();
        AudioStream own = BossMusicStream ?? (!string.IsNullOrEmpty(BossMusicFile) && ResourceLoader.Exists(BossMusicFile) ? GD.Load<AudioStream>(BossMusicFile) : null);
        if (own != null)
        {
            if (own is AudioStreamMP3 mp3) { mp3.Loop = BossMusicLoop; mp3.LoopOffset = BossMusicLoopOffset; }
            else if (own is AudioStreamOggVorbis ogg) { ogg.Loop = BossMusicLoop; ogg.LoopOffset = BossMusicLoopOffset; }
            BossMusic.Stream = own; BossMusic.VolumeDb = SmsActorSound.MusicDb + BossMusicDb + BossMusicStreamDb;
            BossMusicStreamName = BossMusicStream != null ? "stream" : BossMusicFile.GetFile();
        }
        else if (BossMusic.Stream != null) { BossMusic.VolumeDb = (float.IsNaN(_musicBaseDb) ? SmsActorSound.MusicDb : _musicBaseDb) + BossMusicDb; BossMusicStreamName = "casino"; }
        else return;
        BossMusicStarted = true; BossMusicFaded = false;
        BossMusic.Play();
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the BOSS MUSIC starts ({why}): {BossMusicStreamName}, {BossMusicLength:F1} s{(own != null && BossMusicLoop ? $", looping to {BossMusicLoopOffset:F1} s" : "")}, at {BossMusic.VolumeDb:F1} dB");
    }
    /// <summary>The boss music fades out over BossMusicFadeSeconds and stops (the last hit's down; the arena unloading).</summary>
    private void FadeBossMusic(string why)
    {
        if (!BossMusicPlaying || BossMusicFaded) return;
        BossMusicFaded = true;
        var tw = CreateTween(); tw.TweenProperty(BossMusic, "volume_db", -60f, Mathf.Max(0.05f, BossMusicFadeSeconds)); tw.TweenCallback(Callable.From(() => { if (BossMusic != null && IsInstanceValid(BossMusic)) BossMusic.Stop(); }));
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the boss music fades over {BossMusicFadeSeconds:F1} s ({why})");
    }
    private float _musicBaseDb = float.NaN;
    /// <summary>The music player's level as his sound node filled it (SmsActorSound.MusicDb + the track's own), read once before anything changes it.</summary>
    private void ReadMusicBase() { if (float.IsNaN(_musicBaseDb) && BossMusic != null && IsInstanceValid(BossMusic)) _musicBaseDb = BossMusic.VolumeDb; }
    public override void _ExitTree()
    {
        if (_sfxDbSet) { SmsActorSound.MasterDb = _sfxDbWas; _sfxDbSet = false; }                                                            // 195: the effects level put back
        if (BossMusicPlaying) BossMusic.Stop();
        if (!IsCopy && FinalSkyBlend > 0f) SetFinalSky(0f);                                                                                   // 208b: the shared sky material back to the boss-arena sky
        if (!IsCopy && PeaceSkyBlend > 0f) SetPeaceSky(0f);                                                                                   // 217: and off the night
        if (_deathCamPhase != 0) DeathCamEnd();                                                                                               // 221: the picture back to the gameplay camera
        if (_shineReady != null && IsInstanceValid(_shineReady) && !_shineReady.IsQueuedForDeletion()) { _shineReady.QueueFree(); _shineReady = null; }   // 224: a Shine made ahead and never brought out goes with him
        GcQuietEnd("he leaves the tree");                                                                                                     // 226
    }
    /// <summary>The down clip begins (the last hit's hurt is over): the mist and the stars boil off his body, the blink, the glow and the crown's flare run in DeathTick.</summary>
    private void DeathBegin()
    {
        FadeBossMusic("the down");                                                                                                                 // 195
        _deathT = 0f; _deathRumbleT = 0f; _deathHome = Position;
        _deathLen = _anim != null && _anim.HasAnimation(BckDown) ? (float)(_anim.GetAnimation(BckDown).Length / Mathf.Max(0.01f, _anim.SpeedScale)) : 3.3f;
        if (!DeathFx) return;
        _deathOn = true;
        Vector3 at = BodyPoint + Vector3.Up * 1.5f;                                                                                       // the body's middle (the joint sits low in the sphere)
        if (DeathMist > 0) _deathMist = Emitter("DeathMistFx", DeathMist, 1.0, at, Vector3.Up, 180f, 1.5f, 4f, 2.5f, 5f, MistColor, 0.7f, 1.2f, 0.5f, 2.5f);
        if (DeathSparkles > 0 && _chika != null && _chika.ProcessMaterial is ParticleProcessMaterial chikaMat)
        {
            _deathStars = (GpuParticles3D)_chika.Duplicate();
            _deathStars.Name = "DeathStarsFx"; _deathStars.TopLevel = true; _deathStars.Visible = true; _deathStars.Amount = DeathSparkles; _deathStars.Lifetime = 0.9; _deathStars.OneShot = false; _deathStars.Emitting = false; _deathStars.LocalCoords = false;
            var pm = (ParticleProcessMaterial)chikaMat.Duplicate();
            pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere; pm.EmissionSphereRadius = 3.5f; pm.Direction = Vector3.Up; pm.Spread = 180f;
            pm.InitialVelocityMin = 3f; pm.InitialVelocityMax = 7f; pm.Gravity = new Vector3(0f, -3f, 0f); pm.ScaleMin = 1.2f; pm.ScaleMax = 2.4f;
            _deathStars.ProcessMaterial = pm;
            AddChild(_deathStars); _deathStars.GlobalPosition = at; _deathStars.Emitting = true;
        }
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the DEATH's effects on -- the mist and the stars at {at}, the blink at {DeathBlinkHz} Hz, the glow to {DeathGlowBoost} x, the crown {(DeathCrownFlare ? "flaring" : "as is")}");
    }
    /// <summary>A tick of the down clip: the blink, the glow's swell, the crown's flare (quicker and quicker, to the activation flash by the end), the emitters on the
    /// body, the rumble.</summary>
    private void DeathTick(float dt)
    {
        _deathT += dt;
        float k = _deathLen > 0f ? Mathf.Clamp(_deathT / _deathLen, 0f, 1f) : 1f;
        if (DeathSink > 0f)                                                                                                               // the sink: the tumble ends low
        {
            Position = _deathHome + Vector3.Down * (DeathSink * Mathf.SmoothStep(0f, 1f, k));
            float fy = float.IsNaN(_floorAtSpot) ? _introFloorY : _floorAtSpot;
            if (BodyPoint.Y < fy + 2f) Position += Vector3.Up * (fy + 2f - BodyPoint.Y);
        }
        if (!_deathOn) return;
        if (DeathBlinkHz > 0f) { bool flash = ((int)(_deathT * DeathBlinkHz * 2f) & 1) == 0; if (flash != _flashOn) SetFlash(flash); }
        _glowBoost = 1f + (DeathGlowBoost - 1f) * k;
        if (DeathCrownFlare) { float hz = Mathf.Lerp(2f, 9f, k); SetCrownGlow(new Color(1f, 1f, 1f), 0.5f + 0.5f * (0.5f + 0.5f * MathF.Sin(_deathT * hz * MathF.Tau)) + k); }
        Vector3 at = BodyPoint + Vector3.Up * 1.5f;
        if (_deathMist != null && IsInstanceValid(_deathMist)) _deathMist.GlobalPosition = at;
        if (_deathStars != null && IsInstanceValid(_deathStars)) _deathStars.GlobalPosition = at;
        if (DeathRumble > 0f) { _deathRumbleT -= dt; if (_deathRumbleT <= 0f) { _deathRumbleT = 0.4f; SmsCameraShake.Current?.Start(SmsCameraShake.Kind.Damage, DeathRumble); } }
    }
    /// <summary>The down clip is over: he BURSTS where the tumble ended -- the flash, the shock ring on the floor under him, the poof, the stars, the kick, the
    /// afterglow -- the emitters close; the Shine's clock starts. His effects run on through the Shine's stop-the-world (ProcessMode Always till they are done).</summary>
    private void DeathBurst()
    {
        Vector3 at = BodyPoint + Vector3.Up * 1.5f; _burstAt = at; Burst = true;
        _deathOn = false; _glowBoost = 1f; SetFlash(false); SetCrownGlow(_crownGlowColor, 0f);
        if (_deathMist != null && IsInstanceValid(_deathMist)) { var m = _deathMist; m.Emitting = false; _deathFxNodes.Add(m); GetTree().CreateTimer(1.1).Timeout += () => { if (IsInstanceValid(m)) m.QueueFree(); }; _deathMist = null; }   // 223: freed once its 1.0-s wisps are out (183 waited 2.5)
        if (_deathStars != null && IsInstanceValid(_deathStars)) { var s = _deathStars; s.Emitting = false; _deathFxNodes.Add(s); GetTree().CreateTimer(1.0).Timeout += () => { if (IsInstanceValid(s)) s.QueueFree(); }; _deathStars = null; }   // (its 0.9-s stars)
        if (DeathFx)
        {
            float s = DeathFxScale;
            if (DeathFlashSize > 0f) FlashAt("DeathFlash", at, DeathFlashSize, DeathFlashSeconds, flat: false, 0.3f, 1.4f);
            if (DeathFlashSize > 0f && DeathAfterglowSeconds > 0f) FlashAt("DeathAfterglow", at, DeathFlashSize * 0.6f, DeathAfterglowSeconds, flat: false, 1f, 0.5f);   // lingers, shrinking as it fades
            float fy = FloorUnder(at); if (float.IsNaN(fy)) fy = float.IsNaN(_floorAtSpot) ? _introFloorY : _floorAtSpot;
            if (DiveShockRing) ShockRingAt(new Vector3(at.X, fy, at.Z), DiveFlashSize * s, DiveFlashSeconds * 1.6f);
            if (DeathPuffs > 0) _deathFxNodes.Add(DiveBurst("DeathPuffs", DeathPuffs, at, Vector3.Up, 180f, 4f * s, 8f * s, 3f * s, 6f * s, MistColor, 0.85f, 1.3, 3.5f, 0.8f));
            _deathFxNodes.Add(StarBurst("DeathStars", at, DeathStars, s));
            if (DeathShake > 0f) SmsCameraShake.Current?.Start(SmsCameraShake.Kind.Damage, DeathShake);
        }
        ProcessMode = ProcessModeEnum.Always;
        _shinePending = true; _shineT = 0f; _deathFxWaitT = 0f; _deathFxOutAt = -1f;
        BeginPeaceSky();                                                                                                                   // 217: the night comes
        if (!ShineAfterDeathFx) DeathCamBegin();                                                                                           // 221: the camera to the wide shot (223: at the Shine instead, once his effects are out)
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: BURSTS at {at} -- the flash {DeathFlashSize} m, {DeathPuffs} puffs, {DeathStars} stars, the floor at {FloorUnder(at):F2}; the Shine {(ShineAfterDeathFx ? $"{ShineDelaySeconds:F1} s after his effects are out" : $"in {ShineDelaySeconds:F1} s")}");
    }
    /// <summary>223: his death still showing -- a flash (the burst's, its afterglow), the shock ring, the burst's puff and star emitters, the down's mist and
    /// twinkle emitters (each freed once its particles are out).</summary>
    public bool DeathFxAlive => AnyFxAlive || _deathFxNodes.Exists(n => n != null && IsInstanceValid(n) && !n.IsQueuedForDeletion());
    /// <summary>223, for the drives: seconds after the burst at which his effects were out (-1 = not yet), the wait the Shine sat out for them.</summary>
    public float DeathFxOutAt => _deathFxOutAt;
    public float DeathFxWaitSeconds => _deathFxWaitT;
    private readonly List<Node> _deathFxNodes = new(); private float _deathFxWaitT, _deathFxOutAt = -1f;
    /// <summary>Gone: the Shine ShineDelaySeconds after the burst (223: after his effects are out, ShineAfterDeathFx -- the cut to the death camera with it);
    /// once it is there and his last flash is out, back to the world's clock.</summary>
    private void GoneTick(float dt)
    {
        DeathCamTick(dt);                                                                                                                  // 221
        if (_shinePending)
        {
            if (ShineAfterDeathFx && DeathFxAlive) { _deathFxWaitT += dt; return; }                                                       // 223: his death plays out first
            if (_deathFxOutAt < 0f) _deathFxOutAt = _deathFxWaitT;
            _shineT += dt;
            if (_shineT >= ShineDelaySeconds) SpawnShine();
            return;
        }
        _shineT += dt;
        if (ShineSpawned && !AnyFxAlive && _shineT >= ShineDelaySeconds + 1.5f && !PeaceSkyRunning && _deathCamPhase == 0 && ProcessMode == ProcessModeEnum.Always) ProcessMode = ProcessModeEnum.Inherit;   // the Shine's stars done too (217: and the night's fade; 221: and the camera handed back)
    }
    // 221: the death camera (DeathCamWide) -- its phase (0 off, 1 the pan out, 2 the hold, 3 the glide back), its clock, its node and the camera it took the
    // picture from, the pose it eases from and to (a spot, a yaw and a pitch, a fov), the wide shot's spot and look point
    private int _deathCamPhase;
    private float _deathCamT;
    private Camera3D _deathCam, _deathCamWas;
    private Vector3 _dcFromPos, _dcToPos, _dcWidePos, _dcWideLook, _dcWideBase;
    private float _dcFromYaw, _dcFromPitch, _dcFromFov, _dcToYaw, _dcToPitch, _dcToFov, _dcPushedBack;
    /// <summary>221 / 222: the burst -- the camera CUTS to the wide shot (DeathCamWide; DeathCamPanSeconds over 0: a camera of his own takes the picture where the
    /// gameplay camera stands and eases out, smoothstep, the yaw and the pitch turning on their own with no roll): DeathCamDistance m from the arena's middle along
    /// the gameplay camera's own bearing from it, DeathCamHeight m over the floor, looking at DeathCamLookUp m over the middle, at DeathCamFov. The game's own for
    /// Petey (bosspakkun TNerveBPDie: makeShineAppearWithDemo(.., "ボスシャインカメラ", ..)) is a placed fixed camera it cuts to.</summary>
    private void DeathCamBegin()
    {
        if (!DeathCamWide || IsCopy || _deathCamPhase != 0) return;
        var tree = GetTree(); var from = DeathCamGameplay();
        if (tree == null || from == null) return;
        float fy = FloorUnder(_arenaCentre); if (float.IsNaN(fy)) fy = float.IsNaN(_introFloorY) ? _arenaCentre.Y : _introFloorY;
        var mid = new Vector3(_arenaCentre.X, fy, _arenaCentre.Z);
        Vector3 bearing = from.GlobalPosition - mid; bearing.Y = 0f;
        bearing = bearing.LengthSquared() > 1e-4f ? bearing.Normalized() : Vector3.Back;
        _dcWidePos = mid + bearing * DeathCamDistance + Vector3.Up * DeathCamHeight;
        _dcWideLook = mid + Vector3.Up * DeathCamLookUp;
        _dcWideBase = _dcWidePos;
        _deathCamWas = from;
        _deathCam = new Camera3D
        {
            Name = "SmsDeathWideCam", ProcessMode = ProcessModeEnum.Always, Projection = from.Projection, Near = from.Near, Far = Mathf.Max(from.Far, DeathCamDistance * 3f),
            KeepAspect = from.KeepAspect, CullMask = from.CullMask, Environment = from.Environment, Attributes = from.Attributes, HOffset = from.HOffset, VOffset = from.VOffset
        };
        (tree.CurrentScene ?? tree.Root).AddChild(_deathCam);
        DeathCamFit(fy);
        var t = from.GlobalTransform;
        _dcFromPos = t.Origin; (_dcFromYaw, _dcFromPitch) = DeathCamYawPitch(-t.Basis.Z); _dcFromFov = from.Fov;
        _dcToPos = _dcWidePos; (_dcToYaw, _dcToPitch) = DeathCamYawPitch(_dcWideLook - _dcWidePos); _dcToFov = DeathCamFov;
        _deathCamT = 0f; _deathCamPhase = 1;
        DeathCamPose(DeathCamPanSeconds > 0f ? 0f : 1f);
        _deathCam.MakeCurrent();
        SmsStandardHud.CameraDemoBegin(this);                                                                                                // 222: a camera demo for the console: the counters, the lives and the life meter go
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the DEATH CAMERA {(DeathCamPanSeconds > 0f ? $"pans out over {DeathCamPanSeconds:F2} s" : "CUTS")} -- from {from.Name} at {_dcFromPos} (fov {_dcFromFov:F0}) to {_dcWidePos} looking at {_dcWideLook} (fov {DeathCamFov:F0}; {(_dcPushedBack > 0f ? $"pulled back {_dcPushedBack:F0} m to keep the Shine's flight from {_burstAt} in frame" : "the Shine's flight in frame")})");
    }
    /// <summary>221: the wide shot pulled back along its line of sight (2 m at a time, at most DeathCamDistance more) until the Shine's whole flight is in frame
    /// with a margin -- its star's centre through ShineSprite.SmsAppear's rise (SmsUpSpeed x SmsMoveUpTicks) and loop (TShine::movingCircle: the straight line to
    /// its spot plus a hump of SmsCircleRateY x the flat distance), the star's top, bottom and sides (1.5 m x ShineScale, a fifth more). His burst comes where the
    /// down clip leaves his body, 12 m and more from where he hovered: one far out at the edge can leave the default frame; most never move it.</summary>
    private void DeathCamFit(float fy)
    {
        _dcPushedBack = 0f;
        if (DeathCamNode == null) return;
        float k = Mathf.Max(0.05f, ShineScale), upm = SmsUnits.UnitsPerMetre;
        Vector3 placedRoot = ShinePlaced(fy, k), lift = Vector3.Up * (ShineCentreOverRoot * k);
        Vector3 topRoot = (ShineFromBurst ? _burstAt : placedRoot) + Vector3.Up * (ShineSprite.SmsUpSpeed * ShineSprite.SmsMoveUpTicks / upm);
        float flat = new Vector2(placedRoot.X - topRoot.X, placedRoot.Z - topRoot.Z).Length() * upm;                          // in units, as movingCircle has it
        if (flat == 0f) flat = 1000f;
        float yDelta = (placedRoot.Y - topRoot.Y) * upm; if (yDelta > 0f) flat += yDelta;
        float hump = flat * ShineSprite.SmsCircleRateY / upm, half = 0.75f * k * 1.2f;
        var pts = new List<Vector3> { (ShineFromBurst ? _burstAt : placedRoot) + lift, topRoot + lift };
        for (int i = 1; i <= 12; i++) { float t = i / 12f; Vector3 r = topRoot.Lerp(placedRoot, t); r.Y += hump * MathF.Sin(MathF.PI * t); pts.Add(r + lift); }
        Vector3 view = _dcWideLook - _dcWidePos; Vector3 back = -view.Normalized(), right = view.Cross(Vector3.Up).Normalized();
        var (yaw, pitch) = DeathCamYawPitch(view); var basis = Basis.FromEuler(new Vector3(pitch, yaw, 0f));
        var was = _deathCam.GlobalTransform; float wasFov = _deathCam.Fov;
        _deathCam.Fov = DeathCamFov;
        float extra = 0f;
        for (; extra < DeathCamDistance; extra += 2f)
        {
            _deathCam.GlobalTransform = new Transform3D(basis, _dcWidePos + back * extra);
            bool all = true;
            foreach (var c in pts)
                if (!_deathCam.IsPositionInFrustum(c + Vector3.Up * half) || !_deathCam.IsPositionInFrustum(c + Vector3.Down * half) || !_deathCam.IsPositionInFrustum(c + right * half) || !_deathCam.IsPositionInFrustum(c - right * half)) { all = false; break; }
            if (all) break;
        }
        _dcPushedBack = Mathf.Min(extra, DeathCamDistance); _dcWidePos += back * _dcPushedBack;
        _deathCam.GlobalTransform = was; _deathCam.Fov = wasFov;
    }
    /// <summary>221 / 222: a frame of the death camera -- the pan out (none for a cut: the hold at once); the hold (the wide shot fixed while the Shine comes out
    /// where he burst and jumps to the middle; with no demo to wait for -- ShineDemo off, another Shine scene, the Shine gone -- straight on to the release); the
    /// glide back, if one is set, onto the gameplay camera's LIVE pose (the world runs again and it moves), then the picture handed back.</summary>
    private void DeathCamTick(float dt)
    {
        if (_deathCamPhase == 0) return;
        if (DeathCamNode == null) { DeathCamEnd(); return; }
        _deathCamT += dt;
        if (_deathCamPhase == 1)
        {
            float k = DeathCamPanSeconds > 0f ? _deathCamT / DeathCamPanSeconds : 1f;
            DeathCamPose(k);
            if (k >= 1f) { _deathCamPhase = 2; _deathCamT = 0f; }
        }
        else if (_deathCamPhase == 2 && ShineSpawned && !(Shine is ShineSprite s && s.SmsAppearDemoRunning)) DeathCamRelease();
        if (_deathCamPhase == 3)
        {
            var to = DeathCamReturnTo;
            if (to == null) { DeathCamEnd(); return; }
            var t = to.GlobalTransform;
            _dcToPos = t.Origin; (_dcToYaw, _dcToPitch) = DeathCamYawPitch(-t.Basis.Z); _dcToFov = to.Fov;
            float k = DeathCamBackSeconds > 0f ? _deathCamT / DeathCamBackSeconds : 1f;
            DeathCamPose(k);
            if (k >= 1f) { DeathCamEnd(); return; }
        }
        DeathCamKeepView();
    }
    /// <summary>221 / 222: the Shine's demo is over (its SpawnEntranceFinished; or no Shine at all): the camera CUTS back to the gameplay camera -- or, with
    /// DeathCamBackSeconds over 0, the wide shot glides back from where it is onto the gameplay camera's live pose over that long.</summary>
    private void DeathCamRelease()
    {
        if (_deathCamPhase != 1 && _deathCamPhase != 2) return;
        if (DeathCamNode == null || DeathCamReturnTo == null || DeathCamBackSeconds <= 0f) { DeathCamEnd(); return; }
        var t = _deathCam.GlobalTransform;
        _dcFromPos = t.Origin; (_dcFromYaw, _dcFromPitch) = DeathCamYawPitch(-t.Basis.Z); _dcFromFov = _deathCam.Fov;
        _deathCamPhase = 3; _deathCamT = 0f;
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the death camera glides back to {_deathCamWas.Name} over {DeathCamBackSeconds:F2} s");
    }
    /// <summary>221: the death camera off -- the picture back to the gameplay camera (when the view is still the wide shot's, or the shake's of it), the wide
    /// camera freed.</summary>
    private void DeathCamEnd()
    {
        var cam = DeathCamNode; var was = DeathCamReturnTo;
        var cur = IsInsideTree() ? GetViewport()?.GetCamera3D() : null; var shake = SmsCameraShake.Current?.Camera;
        if (was != null && was.IsInsideTree() && (cur == null || cur == cam || (shake != null && cur == shake))) was.MakeCurrent();
        cam?.QueueFree();
        _deathCam = null; _deathCamPhase = 0; _deathCamT = 0f;
        SmsStandardHud.CameraDemoEnd(this);                                                                                                  // 222: and the console comes back (endCameraDemo, three frames on)
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the death camera is over -- the picture {(was != null ? $"back with {was.Name}" : "left as it is (no gameplay camera)")}");
    }
    /// <summary>221: the wide shot's pose k of the way (smoothstep) from the from-pose to the to-pose: the spot straight, the yaw the short way round, the pitch,
    /// the fov.</summary>
    private void DeathCamPose(float k)
    {
        if (DeathCamNode == null) return;
        float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(k, 0f, 1f));
        var basis = Basis.FromEuler(new Vector3(Mathf.Lerp(_dcFromPitch, _dcToPitch, e), Mathf.LerpAngle(_dcFromYaw, _dcToYaw, e), 0f));   // YXZ: the yaw, then the pitch
        _deathCam.GlobalTransform = new Transform3D(basis, _dcFromPos.Lerp(_dcToPos, e));
        _deathCam.Fov = Mathf.Lerp(_dcFromFov, _dcToFov, e);
    }
    /// <summary>221: a view direction's yaw (about +Y, 0 = looking down -Z) and pitch (up positive).</summary>
    private static (float Yaw, float Pitch) DeathCamYawPitch(Vector3 dir)
    {
        dir = dir.LengthSquared() > 1e-8f ? dir.Normalized() : Vector3.Forward;
        return (MathF.Atan2(-dir.X, -dir.Z), MathF.Asin(Mathf.Clamp(dir.Y, -1f, 1f)));
    }
    /// <summary>221: the camera the picture is taken from and handed back to -- the current one, unless that is the shake's render camera (SmsCameraShake renders
    /// whoever made itself current, shaken: his rumble through the down has it current), then the scene's gameplay camera (the Camera3D in the SunshineCamera rig).</summary>
    private Camera3D DeathCamGameplay()
    {
        var cur = GetViewport()?.GetCamera3D(); var shake = SmsCameraShake.Current?.Camera;
        if (cur != null && cur != shake) return cur;
        var scene = GetTree()?.CurrentScene;
        if (scene != null)
            foreach (var c in scene.FindChildren("*", "Camera3D", true, false).OfType<Camera3D>())
            {
                if (c == shake || !c.IsInsideTree()) continue;
                for (Node p = c.GetParent(); p != null; p = p.GetParent()) if (p is SunshineCamera) return c;
            }
        return cur;
    }
    /// <summary>221: the wide shot keeps the picture through its phases -- taken back when nothing has it, or the gameplay camera, or a shake camera that cannot run
    /// (the Shine's stop-the-world froze it mid-shake); a shake that runs renders the wide shot shaken and hands it back itself.</summary>
    private void DeathCamKeepView()
    {
        var cam = DeathCamNode; if (cam == null || !IsInsideTree()) return;
        var cur = GetViewport()?.GetCamera3D();
        if (cur == cam) return;
        var shake = SmsCameraShake.Current;
        if (cur == null || cur == _deathCamWas || (shake != null && cur == shake.Camera && !shake.CanProcess())) cam.MakeCurrent();
    }
    /// <summary>The SHINE (makeShineAppearWithDemo, "the boss shine"): the ShineScene beside him in the arena, placed at ShineSpawnPoint or ShineOverFloor m over the
    /// floor at the arena's middle with its back to Mario, appearing the game's way from where he burst (ShineFromBurst) with the demo (ShineDemo), its exit set
    /// (ShineExitLevel / ShineExitScene), its save flag this scene + his name.</summary>
    /// <summary>224: the Shine's node instanced and added beside him (its _Ready builds its effects and their materials; ShineScale after it, so the effects grow
    /// with it) -- 100 ms of main thread, so MakeShineAhead does it at the fight's start and SpawnShine only places it.</summary>
    private Node3D MakeShine()
    {
        var scene = ShineScene ?? (ResourceLoader.Exists("res://shine_sprite.tscn") ? GD.Load<PackedScene>("res://shine_sprite.tscn") : null);
        if (scene == null) { GD.PushWarning($"[SMS kingboo] {GetPath()}: no ShineScene and no res://shine_sprite.tscn -- no Shine at the fight's end"); return null; }
        var node = scene.Instantiate<Node3D>();
        Node parent = GetParent() ?? GetTree().CurrentScene;
        parent.AddChild(node);
        float k = Mathf.Max(0.05f, ShineScale);
        if (MathF.Abs(k - 1f) > 1e-3f) node.Scale = node.Scale * k;                                                                       // 217: bigger -- after its _Ready, so its effects (built at the scene's scale) grow with it
        return node;
    }
    private Node3D _shineReady;
    public bool ShineMadeAheadNow => _shineReady != null && IsInstanceValid(_shineReady);
    private void MakeShineAhead()
    {
        if (_shineReady != null || ShineSpawned || _shinePending) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var node = MakeShine(); if (node == null) return;
        if (node is ShineSprite s) s.SmsPark(); else node.Visible = false;
        _shineReady = node;
        foreach (string se in new[] { ShineSprite.AppearChimeSe, ShineSprite.HumSe, ShineSprite.GetChimeSe }) SmsActorSound.Stream(se);     // its sounds' streams loaded now too (the first Play loaded them)
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the SHINE made ahead ({node.Name}, parked hidden, no pickup; its three sounds loaded) in {sw.Elapsed.TotalMilliseconds:F1} ms");
    }
    private void SpawnShine()
    {
        _shinePending = false;
        var sw = System.Diagnostics.Stopwatch.StartNew(); double tCam, tInst, tPlace, tAppear;                                          // 224: where the spawn's frame goes
        if (ShineAfterDeathFx) DeathCamBegin();                                                                                            // 223: the cut to the wide shot comes WITH the Shine, his death played out (makeShineAppearWithDemo sets the camera as the Shine appears)
        tCam = sw.Elapsed.TotalMilliseconds;
        bool ahead = _shineReady != null && IsInstanceValid(_shineReady) && !_shineReady.IsQueuedForDeletion();
        var node = ahead ? _shineReady : MakeShine(); _shineReady = null;                                                                 // 224: the one made at the fight's start, else made now
        if (node == null) { DeathCamRelease(); return; }
        tInst = sw.Elapsed.TotalMilliseconds;
        float fy = FloorUnder(_arenaCentre); if (float.IsNaN(fy)) fy = float.IsNaN(_introFloorY) ? _arenaCentre.Y : _introFloorY;
        float k = Mathf.Max(0.05f, ShineScale);
        Vector3 placed = ShinePlaced(fy, k);                                                                                               // 217: the star's centre where the normal one has it
        var m = M(); Vector3 away = m != null ? placed - m.GlobalPosition : Vector3.Forward; away.Y = 0f;
        if (away.LengthSquared() < 1e-4f) away = Vector3.Forward; away = away.Normalized();
        float yaw = Mathf.Atan2(-away.X, -away.Z);                                                                                        // SmsMath.DirFromYaw(yaw) = away: its back to Mario
        if (node is ShineSprite placing) placing.SmsPlaceAt(placed, yaw); else { node.GlobalPosition = placed; node.GlobalRotation = new Vector3(0f, yaw, 0f); }   // 224: the placed spot set outright (a Shine made ahead latched its parking spot on its first frame)
        _shine = node; ShineSpawned = true;
        tPlace = sw.Elapsed.TotalMilliseconds;
        if (node is ShineSprite s)
        {
            if (string.IsNullOrEmpty(s.ShineId)) s.ShineId = $"{GetTree().CurrentScene?.SceneFilePath ?? ""}:{Name}:rematch";              // 118c: the spawned Shine's save flag
            if (ShineExitLevel) { s.ExitLevel = true; if (!string.IsNullOrEmpty(ShineExitScene)) s.ExitScene = ShineExitScene; }
            int ticks = ShineDemoTicks > 0 ? ShineDemoTicks : s.SmsAppearDemoTicks;
            if (ShineCamOffset != Vector3.Zero) s.SmsAppearCamOffset = ShineCamOffset;
            s.SmsAppearLookUp *= k;                                                                                                       // 217: the demo camera on the bigger star's middle
            if (_deathCamPhase != 0) { s.SmsAppearCamera = false; s.SpawnEntranceFinished += DeathCamRelease; }                          // 221: the wide shot watches it; its demo's end sends the camera back
            if (ShineFromBurst) s.SmsAppearFrom(_burstAt, ticks, ShineDemo); else s.SmsAppear(ticks, ShineDemo);
            if (ShineCamFov > 0f && s.SmsAppearCam != null) s.SmsAppearCam.Fov = ShineCamFov;
        }
        tAppear = sw.Elapsed.TotalMilliseconds;
        Vector3 start = ShineFromBurst ? _burstAt : placed;                                                                                // the golden flash and stars where it materialises
        if (ShineFlashSize > 0f) FlashAt("ShineFlash", start, ShineFlashSize, 0.5f, flat: false, 0.4f, 1.3f, ShineFlashColor);
        StarBurst("ShineStars", start, ShineStars, 0.7f, ShineFlashColor);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the SHINE -- {node.Name} placed at {placed} (the floor {fy:F2}, Mario {(m != null ? Flat(placed, m.GlobalPosition).ToString("F1") : "?")} m off), appearing from {(ShineFromBurst ? _burstAt.ToString() : "its spot")}, the demo {ShineDemo}");
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the SHINE's spawn took {sw.Elapsed.TotalMilliseconds:F1} ms on the main thread ({(ahead ? "the Shine made ahead" : "the Shine made now")}) -- the death camera {tCam:F1}, {(ahead ? "taking the made one" : "its instantiate and add (its _Ready: the effects and their materials)")} {tInst - tCam:F1}, the placing {tPlace - tInst:F1}, the appear (its sounds, the stop-the-world, the HUD) {tAppear - tPlace:F1}, the flash and the stars {sw.Elapsed.TotalMilliseconds - tAppear:F1}");
        EmitSignal(SignalName.ShineAppearing, node);
    }
    /// <summary>A continuous emitter of the port's smoke ball (mix-blended) at a point: count alive, their life, a direction and spread, speeds, sizes, colour, alpha,
    /// damping, gravity; the rise's wisps. Freed by the caller.</summary>
    private GpuParticles3D Emitter(string name, int count, double life, Vector3 at, Vector3 dir, float spread, float v0, float v1, float s0, float s1, Color col, float alpha, float damp, float gravity, float radius)
    {
        var scale = new Curve { MinValue = 0f, MaxValue = Mathf.Max(1f, Mathf.Max(s0, s1) * 1.1f) }; scale.AddPoint(new Vector2(0f, s0)); scale.AddPoint(new Vector2(1f, s1));
        var fade = new Gradient { Offsets = new float[] { 0f, 0.2f, 0.7f, 1f }, Colors = new Color[] { new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, alpha), new Color(1f, 1f, 1f, alpha * 0.6f), new Color(1f, 1f, 1f, 0f) } };
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = radius, Direction = dir.Normalized(), Spread = spread, InitialVelocityMin = v0, InitialVelocityMax = v1,
            Gravity = new Vector3(0f, gravity, 0f), DampingMin = damp, DampingMax = damp * 1.6f, ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = new CurveTexture { Curve = scale },
            AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -50f, AngularVelocityMax = 50f, Color = new Color(col.R, col.G, col.B, 1f), ColorRamp = new GradientTexture1D { Gradient = fade }
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = FxTex("kemuball3_raw"), DisableReceiveShadows = true };
        var p = new GpuParticles3D { Name = name, TopLevel = true, Amount = count, Lifetime = life, OneShot = false, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-10f, -4f, -10f), new Vector3(20f, 14f, 20f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(p); p.GlobalPosition = at; p.Emitting = true;
        return p;
    }
    /// <summary>A one-shot burst of the port's smoke ball (mix-blended): count, from a point, a direction and spread, speeds, sizes, colour, alpha, life, damping, gravity.</summary>
    private GpuParticles3D DiveBurst(string name, int count, Vector3 at, Vector3 dir, float spread, float v0, float v1, float s0, float s1, Color col, float alpha, double life, float damp, float gravity, bool flat = false)
    {
        var scale = new Curve { MinValue = 0f, MaxValue = Mathf.Max(1f, Mathf.Max(s0, s1) * 1.1f) }; scale.AddPoint(new Vector2(0f, s0)); scale.AddPoint(new Vector2(1f, s1));
        var fade = new Gradient { Offsets = new float[] { 0f, 0.1f, 0.65f, 1f }, Colors = new Color[] { new Color(1f, 1f, 1f, alpha), new Color(1f, 1f, 1f, alpha), new Color(1f, 1f, 1f, alpha * 0.55f), new Color(1f, 1f, 1f, 0f) } };
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = flat ? 0.8f : 0.5f,
            Direction = dir.Normalized(), Spread = spread, Flatness = flat ? 1f : 0f, InitialVelocityMin = v0, InitialVelocityMax = v1, Gravity = new Vector3(0f, gravity, 0f), DampingMin = damp, DampingMax = damp * 1.6f,
            ScaleMin = 1f, ScaleMax = 1f, ScaleCurve = new CurveTexture { Curve = scale }, AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -70f, AngularVelocityMax = 70f,
            Color = new Color(col.R, col.G, col.B, 1f), ColorRamp = new GradientTexture1D { Gradient = fade }
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = FxTex("kemuball3_raw"), DisableReceiveShadows = true };
        var p = new GpuParticles3D { Name = name, TopLevel = true, Amount = count, Lifetime = life, OneShot = true, Explosiveness = 0.9f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-14f, -4f, -14f), new Vector3(28f, 14f, 28f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(p); p.GlobalPosition = at;
        p.Finished += p.QueueFree; p.Emitting = true;
        return p;
    }
    private static Texture2D FxTex(string name) { string p = $"res://assets/Sms/Fx/tex/{name}.png"; return ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : null; }

    /// <summary>174: the quiet spawn's appear clip ended before the rise did -- the wait begins once he is up.</summary>
    private void QuietReadyWhenUp()
    {
        if (State != Phase.Appear) return;
        if (_spawnRise) { GetTree().CreateTimer(0.05f).Timeout += QuietReadyWhenUp; return; }
        Position = _home;
        EnterWait(FirstAttackDelay);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: up at his spot -- waits, the fight may start (the first move in {FirstAttackDelay:F1} s)");
        EmitSignal(SignalName.ReadyForFight);
    }

    /// <summary>162: his hover height over the floor from where he was put, the arena's middle and radius from its floor (SmsArenaFloor), for the spots.</summary>
    private void ReadArena()
    {
        float fy = FloorUnder(GlobalPosition);
        _hoverOverFloor = float.IsNaN(fy) ? 4f : Mathf.Max(0.5f, GlobalPosition.Y - fy);
        _arenaCentre = GlobalPosition; _arenaRadius = 12f; _stoneRadius = 12f;
        var floor = GetTree().CurrentScene?.FindChildren("*", "StaticBody3D", true, false).OfType<SmsArenaFloor>().FirstOrDefault();
        if (floor != null && floor.Radius > 1f) { _arenaCentre = floor.Centre; _arenaRadius = floor.Radius; _stoneRadius = floor.StoneRadius > 1f ? floor.StoneRadius : floor.Radius; }   // 229: the stone's own edge for the volley's wall
        _arenaRead = true;
    }
    /// <summary>The ground under a point (a ray from 100 m over it, 400 m down, Mario left out): its world y, or NaN.</summary>
    private float FloorUnder(Vector3 p)
    {
        var space = GetWorld3D()?.DirectSpaceState; if (space == null) return float.NaN;
        var q = PhysicsRayQueryParameters3D.Create(p + Vector3.Up * 100f, p + Vector3.Down * 300f);
        q.CollideWithAreas = false; q.CollideWithBodies = true;
        var m = M(); if (m != null) q.Exclude = new Godot.Collections.Array<Rid> { m.GetRid() };
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? hit["position"].AsVector3().Y : float.NaN;
    }
    private Mario M()
    {
        if (_mario == null || !IsInstanceValid(_mario)) _mario = GetTree().GetFirstNodeInGroup("player") as Mario;
        return _mario;
    }
    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>A clip from its top at the bck's own rate. 198 (Jaysonn: "a setting for King Boo's animations to blend together a little better?"): the new clip
    /// CROSS-FADES from the pose he is in over ClipBlendSeconds (AnimationPlayer's custom blend: the old pose fades out as the new clip plays from its first frame) --
    /// the game cuts between bcks hard; 0 = that cut. The first clip of all, and any clip starting while he is unseen, cuts (nothing to blend from).</summary>
    private void PlayClip(string name)
    {
        if (_anim == null || !_anim.HasAnimation(name)) { if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: no clip {name}"); CallDeferred(nameof(OnClipEnd), new StringName(name)); return; }
        var clip = _anim.GetAnimation(name);
        var (frames, loop) = Clips.TryGetValue(name, out var c) ? c : (60, false);
        clip.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        float rate = clip.Length > 0f ? frames / (float)clip.Length : 60f;
        bool seen = _model != null && _model.Visible && _opacityShown > 0.02f;
        double blend = ClipBlendSeconds > 0f && _clipsPlayed > 0 && seen ? ClipBlendSeconds : -1.0;
        if (blend > 0.0) { _anim.Play(name, blend); ClipBlends++; }
        else { _anim.Play(name); _anim.Seek(0f, true); }
        _clipsPlayed++;
        _anim.SpeedScale = 60f / rate;
    }
    private int _clipsPlayed;
    /// <summary>198, for the drives: how many clip starts cross-faded, of how many starts.</summary>
    public int ClipBlends { get; private set; }
    public int ClipStarts => _clipsPlayed;
    /// <summary>The bck frame the clip named is at now (a big number when it is not the one playing).</summary>
    private float ClipFrameNow(string name)
    {
        if (_anim == null || !_anim.IsPlaying()) return 9999f;
        string cur = _anim.CurrentAnimation;
        if (string.IsNullOrEmpty(cur) || cur != name) return 9999f;
        var clip = _anim.GetAnimation(cur);
        int frames = Clips.TryGetValue(cur, out var c) ? c.frames : 60;
        return clip.Length > 0f ? (float)(_anim.CurrentAnimationPosition / clip.Length) * frames : 9999f;
    }

    private void OnClipEnd(StringName name)
    {
        string n = name;
        if (State == Phase.Appear && n == BckAppear)
        {
            if (_spawnQuiet)                                                                       // 174: the intro had the laugh -- the wait (the rise finishes under it)
            {
                _spawnQuiet = false; if (_spawnRise && _introT < (_riseClipStarted ? _riseLen : SpawnRiseSeconds)) { PlayClip(BckWait); CallDeferred(nameof(QuietReadyWhenUp)); return; }
                _spawnRise = false; Position = _home;
                EnterWait(FirstAttackDelay);
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: up at his spot -- waits, the fight may start (the first move in {FirstAttackDelay:F1} s)");
                EmitSignal(SignalName.ReadyForFight);
                return;
            }
            State = Phase.Laugh; _laughSounded = false;
            PlayClip(BckLaugh);
            if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the laugh");
            EmitSignal(SignalName.Laughed);
        }
        else if (State == Phase.IntroLaugh && n == BckLaugh) { PlayClip(_introT + 1.5f <= IntroLaughLength ? BckLaugh : BckWait); }   // 174: the laugh loops while the intro laughs
        else if (State == Phase.Split && n == BckLaugh) PlayClip(BckWait);                                                                // 214 (Jaysonn: "he kinda pauses mid air"): a laugh over in the split's line -- back to the wait clip, never frozen in its last pose (run split214b: the real one held it 0.75 s, a copy 0.15 s, till the cut)
        else if (State == Phase.Mock && n == BckLaugh) PlayClip(BckWait);                                                                 // 191: a long mock outlasts the laugh clip
        else if (State == Phase.Laugh && n == BckLaugh && !_revealing)
        {
            EnterWait(FirstAttackDelay);
            if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: waits -- the fight may start (the first move in {FirstAttackDelay:F1} s)");
            EmitSignal(SignalName.ReadyForFight);
        }
        else if (State == Phase.Hurt && (n == BckHit || n == HurtClip))
        {
            SetFlash(false); Rotation = new Vector3(0f, _yaw, 0f);
            if (Hp > 0 || IsCopy)
            {
                if (_copyPending)                                                                                   // 177: the split
                {
                    _copyPending = false;
                    if (SplitCutscene && !IsCopy && BeginSplitCutscene()) return;                                       // 188: the split's cutscene -- the line of him, the laugh, the card; the fight resumes after
                    SpawnCopy();
                }
                if (TeleportAfterHit)                                                                               // 178: the "reset" -- away to another spot, a full gap there
                {
                    _afterHit = true; _moveIsLick = false; State = Phase.Vanish; _fadeT = 0f; MovesMade++;
                    if (!string.IsNullOrEmpty(TeleportClip)) PlayClip(TeleportClip);
                    if (_sound != null && !string.IsNullOrEmpty(VanishSe)) _sound.Play(VanishSe);
                    if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the hurt is over{(IsCopy ? "" : $", {Hp} hit point(s) left")} -- vanishes to another spot");
                }
                else { EnterWait(NextGap()); if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: back to the wait, {Hp} hit point(s) left, the next move in {_gapT:F1} s"); }
            }
            else
            {
                var swDown = System.Diagnostics.Stopwatch.StartNew(); double tClip, tSe, tCopies;                                   // 226: where the down's frame goes (it ran 58-68 ms)
                State = Phase.Down; PlayClip(BckDown);
                tClip = swDown.Elapsed.TotalMilliseconds;
                if (_sound != null && !string.IsNullOrEmpty(DownSe)) _sound.Play(DownSe);
                tSe = swDown.Elapsed.TotalMilliseconds;
                foreach (var c in _copies.ToArray()) if (c != null && IsInstanceValid(c) && c.State != Phase.Gone) c.DismissCopy();   // 177: his copies go with him
                tCopies = swDown.Elapsed.TotalMilliseconds;
                DeathBegin();                                                                                                        // 183: the mist, the stars, the blink, the glow and the crown through the tumble
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: DOWN ({_deathLen:F2} s of the down clip, then the burst)");
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the DOWN's entry took {swDown.Elapsed.TotalMilliseconds:F1} ms on the main thread -- the down clip {tClip:F1}, its sound {tSe - tClip:F1}, the copies dismissed {tCopies - tSe:F1}, DeathBegin (the music fade, the mist, the stars) {swDown.Elapsed.TotalMilliseconds - tCopies:F1}");
            }
        }
        else if (State == Phase.Down && n == BckDown)
        {
            DeathBurst();                                                                                                            // 183: he bursts where the tumble ended; the Shine follows
            State = Phase.Gone;
            if (_model != null) _model.Visible = false;
            PlaceFx();
            if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: gone -- defeated; the Shine in {ShineDelaySeconds:F1} s");
            EmitSignal(SignalName.Defeated);
        }
        else if (_revealing && n == BckLaugh) { State = Phase.Vanish; _fadeT = 0f; if (_sound != null && !string.IsNullOrEmpty(VanishSe)) _sound.Play(VanishSe); }   // 177: the found-out copy's laugh is over: it fades (181: a poof at the end of the fade)
        else if (State == Phase.Lick && n == LickClip) _lickClipEnded = true;
        else if (State == Phase.Smash && n == SmashClip) { if (_smashLaunchedCount < SmashCount) PlayClip(SmashClip); else _smashClipEnded = true; }
        else if (State == Phase.Lightning && n == LightningClip) { if (!_lightningDone) PlayClip(LightningClip); }
        else if (State == Phase.Breath && n == BreathClip) { _breathClipHeld = false; PlayClip(BckWait); }
        else if (State == Phase.Fireballs && n == FireballClip) { _fireClipHeld = false; PlayClip(BckWait); }
        else if (State == Phase.Volley && n == VolleyClip) { _volleyClipHeld = false; PlayClip(BckWait); }   // 200
        else if (State == Phase.Circle && n == CircleClipNow) { if (!_circleLaunched) PlayClip(CircleClipNow); else PlayClip(BckWait); }   // 201: the laugh through the tell, the wait after
        else if (State == Phase.Ambush && n == BckAppear && _ambushStage == 3) PlayClip(BckWait);                                              // 201: up out of the floor
        else if (State == Phase.Wind && n == WindClip) { if (!_windLaunched) PlayClip(WindClip); else PlayClip(BckWait); }
    }

    private void EnterWait(float gap)
    {
        State = Phase.Wait; _hoverT = 0f; Position = _home; _gapT = gap; LastGap = gap;
        PlayClip(BckWait);
    }
    /// <summary>The gap before the next move: GapAtStart with every hit point, GapAtLastHit at his last one, give or take GapJitter (a copy: CopyGapScale times it).</summary>
    private float NextGap()
    {
        float k = HitPoints > 1 ? Mathf.Clamp((IsCopy && Original != null ? Original.HitsTaken : HitsTaken) / (float)(HitPoints - 1), 0f, 1f) : 1f;
        float g = Mathf.Lerp(GapAtStart, GapAtLastHit, k) + ((float)_rng.NextDouble() * 2f - 1f) * GapJitter;
        return Mathf.Max(0.3f, g) * (IsCopy ? Mathf.Max(0.2f, CopyGapScale) : 1f);
    }

    // ---- 177: the copies ----------------------------------------------------------------------------------------------------------------------
    /// <summary>A copy of him splits off: the flash on him, and a duplicate of this node (the scene's knobs with it) fading in at a far spot, laughing. The
    /// copy is this class with IsCopy: hits without damage (178), far spots, long gaps, the throws alone, the tell (ApplyTell).</summary>
    private void SpawnCopy()
    {
        if (IsCopy || GetParent() == null) return;
        var copy = Duplicate((int)(DuplicateFlags.Scripts | DuplicateFlags.Groups | DuplicateFlags.UseInstantiation)) as SmsKingBooRematch;
        if (copy == null) { GD.PushWarning($"[SMS kingboo] {Name}: the copy could not be made"); return; }
        CopiesMade++;
        copy.Name = $"{Name}Copy{CopiesMade}"; copy.IsCopy = true; copy.Original = this;
        GetParent().AddChild(copy);
        copy.GlobalTransform = GlobalTransform;
        copy.BeginAsCopy(_hoverOverFloor, _opacitySolid);
        _copies.Add(copy);
        SetFlash(true); _splitFlashT = 0.25f;
        if (_sound != null && !string.IsNullOrEmpty(LaughSe)) _sound.Play(LaughSe, LaughDb);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: a COPY splits off ({copy.Name}; {CopiesAlive} out, {CopiesMade} made) after hit {HitsTaken}");
        EmitSignal(SignalName.CopyMade, copy);
    }
    private float _splitFlashT;
    /// <summary>177: a copy throws at CopySpeedScale of his speed (the lava balls' pops and roll, the breath's sweep and stream); the real one at 1.</summary>
    private float ThrowSpeedScale => IsCopy ? Mathf.Clamp(CopySpeedScale, 0.2f, 2f) : 1f;
    /// <summary>The copy's start (on the duplicate, after it is in the tree): his hover and solidity, then it teleports in at a far spot like one of his moves.</summary>
    private void BeginAsCopy(float hoverOverFloor, float opacity)
    {
        Hp = 1; HitsTaken = 0;
        ReadArena(); _hoverOverFloor = hoverOverFloor; _opacitySolid = opacity; _home = Position;
        if (_model != null) _model.Visible = true;
        if (_tongue != null) { _tongue.Stretch = TongueStretch; _tongue.On = TongueStretch > 1.001f; }
        ApplyTell();                                                                                        // 178: the crown's colour / the fangs
        SetCrownGlow(_crownGlowColor, 0f);
        ArriveAt(RandomSpot());
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the copy arrives at {_dest} ({(M() != null ? Flat(_dest, M().GlobalPosition).ToString("F1") : "?")} m from Mario); the tell {CopyTell}");
    }
    // ---- 188: THE SPLIT'S CUTSCENE -- the hooks the cutscene node drives ----------------------------------------------------------------------------------
    /// <summary>188, for the drives: the cutscene running now (the real one's).</summary>
    public SmsSplitCutscene SplitNow => _split != null && IsInstanceValid(_split) ? _split : null;
    public int SplitsShown { get; private set; }
    private SmsSplitCutscene _split;
    /// <summary>The hurt that earns a copy is over: the cutscene node made and begun (it makes the copy itself). False = no cutscene (no Mario / no parent): 177's split.</summary>
    private bool BeginSplitCutscene()
    {
        var m = M(); if (m == null || GetParent() == null) return false;
        if (_split != null && IsInstanceValid(_split)) return false;
        var cs = new SmsSplitCutscene { Name = $"SplitCutscene{SplitsShown + 1}", DebugPrint = DebugPrint };
        (GetTree().CurrentScene ?? GetParent()).AddChild(cs);
        _split = cs; SplitsShown++;
        cs.Begin(this, m);
        if (FinalSky && SplitsShown == FinalSkyAtSplit) BeginFinalSky();                                                                       // 208b: the final phase's sky
        return true;
    }
    // ---- 208b: THE FINAL PHASE's SKY ------------------------------------------------------------------------------------------------------------------------------
    /// <summary>208b, for the drives: how far the sky has gone over to the final-boss sky (0 = the boss-arena sky, 1 = all the way), whether the fade has begun.</summary>
    public float FinalSkyBlend { get; private set; }
    public bool FinalSkyBegun { get; private set; }
    public bool FinalSkyMaterialFound => SkyMaterial() != null;
    private ShaderMaterial _skyMat; private bool _skyLooked; private float _finalSkyT = -1f;
    private ShaderMaterial SkyMaterial()
    {
        if (_skyLooked) return _skyMat;
        _skyLooked = true;
        var we = GetTree()?.CurrentScene?.FindChildren("*", "WorldEnvironment", true, false).OfType<WorldEnvironment>().FirstOrDefault();
        _skyMat = we?.Environment?.Sky?.SkyMaterial as ShaderMaterial;
        return _skyMat;
    }
    /// <summary>The sky's blend set on the shared material (it is a resource: the real one puts it back to 0 when he arrives and when he leaves).</summary>
    private void SetFinalSky(float k)
    {
        FinalSkyBlend = k;
        var mat = SkyMaterial(); if (mat != null) mat.SetShaderParameter("final_blend", k);
    }
    private void BeginFinalSky() { if (FinalSkyBegun) return; FinalSkyBegun = true; _finalSkyT = 0f; FinalSkyTick(0f); if (DebugPrint) GD.Print($"[SMS rematch] the FINAL PHASE's sky begins (split {SplitsShown}, over {FinalSkyBlendSeconds:F1} s; material {(SkyMaterial() != null ? "found" : "NOT FOUND")})"); }
    /// <summary>Ticked by the real one's _Process and, while the world is frozen for the cutscene, by the cutscene node.</summary>
    public void FinalSkyTick(float dt)
    {
        if (_finalSkyT < 0f) return;
        _finalSkyT += dt;
        float k = FinalSkyBlendSeconds <= 0.01f ? 1f : Mathf.Clamp(_finalSkyT / FinalSkyBlendSeconds, 0f, 1f);
        SetFinalSky(k * k * (3f - 2f * k));
        if (k >= 1f) _finalSkyT = -1f;
    }
    /// <summary>For the drives / a restart: the sky back to the boss-arena sky at once.</summary>
    public void ResetFinalSky() { _finalSkyT = -1f; FinalSkyBegun = false; SetFinalSky(0f); _peaceSkyT = -1f; PeaceSkyBegun = false; SetPeaceSky(0f); }   // 217: and the night's
    // ---- 217: the peaceful night after the defeat (PeaceSky)
    public float PeaceSkyBlend { get; private set; }
    public bool PeaceSkyBegun { get; private set; }
    public bool PeaceSkyRunning => _peaceSkyT >= 0f && _peaceSkyT < PeaceSkySeconds;
    private float _peaceSkyT = -1f;
    /// <summary>217: the Shine scene's star centre over its root at its own scale (SmsStarLocal (0, 200, 0) x 0.006 = 1.2 m) -- a bigger Shine is lowered by
    /// this x (ShineScale - 1) so its centre stays at the normal one's height.</summary>
    private const float ShineCentreOverRoot = 1.2f;
    /// <summary>The Shine's placed root: ShineSpawnPoint, else ShineOverFloor over the floor fy at the arena's middle, lowered ShineCentreOverRoot x (k - 1) for its
    /// size k (217: the star's centre where the normal one has it). 221: SpawnShine places it there; the death camera frames its flight to there.</summary>
    private Vector3 ShinePlaced(float fy, float k) => ShineSpawnPoint != null ? ShineSpawnPoint.GlobalPosition : new Vector3(_arenaCentre.X, fy + ShineOverFloor - ShineCentreOverRoot * (k - 1f), _arenaCentre.Z);
    private void SetPeaceSky(float k)
    {
        PeaceSkyBlend = k;
        var mat = SkyMaterial(); if (mat != null) mat.SetShaderParameter("peace_blend", k);
    }
    private void BeginPeaceSky()
    {
        if (!PeaceSky || PeaceSkyBegun || IsCopy) return;
        PeaceSkyBegun = true; _peaceSkyT = 0f; PeaceSkyTick(0f);
        if (DebugPrint) GD.Print($"[SMS rematch] the PEACEFUL night begins (over {PeaceSkySeconds:F1} s; the sky material {(SkyMaterial() != null ? "found" : "NOT FOUND")})");
    }
    public void PeaceSkyTick(float dt)
    {
        if (_peaceSkyT < 0f) return;
        _peaceSkyT += dt;
        float k = PeaceSkySeconds <= 0.01f ? 1f : Mathf.Clamp(_peaceSkyT / PeaceSkySeconds, 0f, 1f);
        SetPeaceSky(k * k * (3f - 2f * k));
    }
    /// <summary>Into the Split phase: every move called off, the flash off, upright; the node waits for the cutscene's calls.</summary>
    public void SplitBegin()
    {
        AbortLick(); AbortSmash(); AbortLightning(); AbortBreath(); AbortFireballs(); AbortWind(); AbortVolley(); AbortAmbush(); AbortCircle();
        SetFlash(false); _splitFlashT = 0f; Rotation = new Vector3(0f, _yaw, 0f);
        State = Phase.Split; _gapT = 0f;
        PlayClip(BckWait);
    }
    /// <summary>Gone in a poof where he is (181's teleport-out effects), unseen.</summary>
    public void SplitHide()
    {
        if (_opacityShown > 0.02f && _model != null && _model.Visible) TeleportOutFx();
        SetOpacity(0f);
        if (_model != null) _model.Visible = false;                                                         // the ghost shader draws BLACK at opacity 0 (run bo: a black silhouette with a crown)
    }
    /// <summary>Put at his place in the line, facing Mario, the arrival's flash and poof there (teleportFx; 194: off -- the cutscene's gather draws its own); shown by SplitFade.</summary>
    public void SplitShowAt(Vector3 dest, bool teleportFx = true)
    {
        Position = dest; _home = dest; _dest = dest; LastSpot = dest;
        FaceNow();
        PlayClip(BckWait);
        if (teleportFx) TeleportInFx();
        if (_sound != null && !string.IsNullOrEmpty(ArriveSe)) _sound.Play(ArriveSe);
    }
    /// <summary>194: the mist GATHERING into one of him coming into the line at `at` (the hover point): puffs from a shell SplitGatherRadius out drawn into his middle
    /// over SplitFadeSeconds, the ring of cold light racing out over the stone under him, the glow there. The cutscene fades the body in over the same time.</summary>
    public void SplitGather(Vector3 at)
    {
        float floorY = FloorUnder(at); if (float.IsNaN(floorY)) floorY = at.Y - _hoverOverFloor;
        Vector3 body = at + Vector3.Up * SplitBodyCentreLift, floor = new Vector3(at.X, floorY, at.Z);
        float seconds = Mathf.Max(0.2f, SplitFadeSeconds);
        if (SplitGatherPuffs > 0) GatherBurst("SplitGather", SplitGatherPuffs, body, SplitGatherRadius, seconds, SplitMistColor);
        if (SplitGatherStars > 0) GatherStars("SplitGatherStars", SplitGatherStars, body, SplitGatherRadius * 1.1f, seconds);
        if (SplitRing && SplitRingRadius > 1f) SmsSmashFx.Ring(this, floor, 1.5f, SplitRingRadius, Mathf.Max(0.2f, SplitRingSeconds), new Color(SplitMistColor.R, SplitMistColor.G, SplitMistColor.B, 0.9f));
        FlashAt("SplitGlow", floor, 14f, seconds + 0.3f, flat: true, 0.4f, 1f, SplitMistColor);
        SplitGathers++;
    }
    /// <summary>194: the POP as he turns solid -- the mist boiling back out of him (SplitMist) and a flash at his middle.</summary>
    public void SplitPop(Vector3 at)
    {
        SplitMist(at);
        FlashAt("SplitPop", at + Vector3.Up * SplitBodyCentreLift, 10f, 0.3f, flat: false, 0.6f, 1.5f, SplitMistColor);
        SplitPops++;
    }
    /// <summary>194 (Jaysonn: "the white puffs are going in at the top of King Boo, needs to be his centre"): his body's CENTRE over the node, read off the WAIT pose as he
    /// waits (the body joint over the node, plus the hit cylinder's middle over the joint: (HitAbove - HitBelow) / 2) -- the gather, the pop and the boil are centred there.
    /// Before it is read (never waited yet) the knob SplitBodyLiftGuess stands in; 188-194a put them a fixed 3 m over the node, the top of him.</summary>
    [Export(PropertyHint.Range, "-3,10,0.1")] public float SplitBodyLiftGuess = 0f;   // measured (drive dc): the joint sits 0.14 m UNDER the node in the wait, the body -2.6..+4.4 about it
    public float WaitBodyLift { get; private set; } = float.NaN;
    public float SplitBodyCentreLift => (float.IsNaN(WaitBodyLift) ? SplitBodyLiftGuess : WaitBodyLift) + (HitAbove - HitBelow) * 0.5f;
    /// <summary>194, for the drives: the gathers and the pops this node has drawn.</summary>
    public int SplitGathers { get; private set; }
    public int SplitPops { get; private set; }
    /// <summary>194: his twinkle stars drawn INWARD with the mist -- born on a shell `radius` out, streaming to `centre` over `seconds`: the sparkle of him gathering.</summary>
    private void GatherStars(string name, int count, Vector3 centre, float radius, float seconds)
    {
        if (count <= 0 || _chika == null || _chika.ProcessMaterial is not ParticleProcessMaterial chikaMat) return;
        var p = (GpuParticles3D)_chika.Duplicate();
        p.Name = name; p.TopLevel = true; p.Visible = true; p.Amount = count; p.Lifetime = seconds; p.OneShot = true; p.Explosiveness = 0.5f; p.Emitting = false; p.LocalCoords = false;
        var pm = (ParticleProcessMaterial)chikaMat.Duplicate();
        pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.SphereSurface; pm.EmissionSphereRadius = Mathf.Max(1f, radius);
        pm.Direction = Vector3.Up; pm.Spread = 0f; pm.InitialVelocityMin = 0f; pm.InitialVelocityMax = 0f; pm.Gravity = Vector3.Zero;
        pm.RadialVelocityMin = -radius / seconds * 1.3f; pm.RadialVelocityMax = -radius / seconds * 0.8f;
        pm.ScaleMin = 1.4f; pm.ScaleMax = 2.6f;
        pm.Color = new Color(SplitMistColor.R, SplitMistColor.G, SplitMistColor.B, 1f);
        p.ProcessMaterial = pm; p.VisibilityAabb = new Aabb(new Vector3(-24f, -12f, -24f), new Vector3(48f, 30f, 48f));
        AddChild(p); p.GlobalPosition = centre; p.Finished += p.QueueFree; p.Emitting = true;
    }
    /// <summary>194: a one-shot of the smoke ball drawn INWARD -- `count` puffs born on a shell `radius` out from `centre`, flying to it over `seconds` (the radial
    /// velocity, negative), shrinking and fading as they arrive: the mist gathering into a body.</summary>
    private void GatherBurst(string name, int count, Vector3 centre, float radius, float seconds, Color col)
    {
        var scale = new Curve { MinValue = 0f, MaxValue = 6f }; scale.AddPoint(new Vector2(0f, 4.5f)); scale.AddPoint(new Vector2(1f, 1.4f));                  // big soft puffs overlapping (v194-2's 2.4 -> 0.7 read as a swarm of beads), shrinking as they close in
        var fade = new Gradient { Offsets = new float[] { 0f, 0.15f, 0.75f, 1f }, Colors = new Color[] { new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0f) } };
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.SphereSurface, EmissionSphereRadius = Mathf.Max(1f, radius),
            Direction = Vector3.Up, Spread = 0f, InitialVelocityMin = 0f, InitialVelocityMax = 0f, Gravity = Vector3.Zero,
            RadialVelocityMin = -radius / seconds * 1.15f, RadialVelocityMax = -radius / seconds * 0.7f,                                       // inward: the shell to the middle in about `seconds` -- the speeds spread, so it is a stream, not one clean shell (v194-1 read as rings of beads)
            ScaleMin = 0.6f, ScaleMax = 1.3f, ScaleCurve = new CurveTexture { Curve = scale }, AngleMin = -180f, AngleMax = 180f, AngularVelocityMin = -90f, AngularVelocityMax = 90f,
            Color = new Color(col.R, col.G, col.B, 1f), ColorRamp = new GradientTexture1D { Gradient = fade }
        };
        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, AlbedoTexture = FxTex("kemuball3_raw"), DisableReceiveShadows = true };
        var p = new GpuParticles3D { Name = name, TopLevel = true, Amount = count, Lifetime = seconds, OneShot = true, Explosiveness = 0.6f, Emitting = false, LocalCoords = false, FixedFps = 60, Interpolate = true, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = pm, DrawPass1 = new QuadMesh { Size = Vector2.One, Material = mat }, VisibilityAabb = new Aabb(new Vector3(-24f, -12f, -24f), new Vector3(48f, 30f, 48f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(p); p.GlobalPosition = centre;
        p.Finished += p.QueueFree; p.Emitting = true;
    }
    /// <summary>His opacity through the arrival: 0..1 of his solid -- the crown with the body (SetCrownGlow leaves the crown solid), the model hidden at 0.</summary>
    public void SplitFade(float k)
    {
        k = Mathf.Clamp(k, 0f, 1f);
        SetOpacity(_opacitySolid * k);
        _crownMat?.SetShaderParameter("opacity", Mathf.Lerp(_opacitySolid, 1f, _crownGlowK) * k);
        if (_model != null) _model.Visible = k > 0.02f;
    }
    /// <summary>The laugh clip and the laugh, at a pitch (1 = his; a copy a shade off: the chorus).</summary>
    public void SplitLaugh(float pitch = 1f)
    {
        PlayClip(BckLaugh);
        if (_sound != null && !string.IsNullOrEmpty(LaughSe)) _sound.Play(LaughSe, LaughDb, Mathf.Clamp(pitch, 0.5f, 2f));
    }
    /// <summary>Turned on Mario at once.</summary>
    public void SplitFace() => FaceNow();
    /// <summary>The ghost mist boiling up where one of him comes into the line (SplitMistColor -- 193: a cold white, violet before; SplitMistPuffs; the dive's smoke balls).</summary>
    public void SplitMist(Vector3 at)
    {
        if (SplitMistPuffs <= 0) return;
        DiveBurst("SplitMist", SplitMistPuffs, at + Vector3.Up * SplitBodyCentreLift, Vector3.Up, 170f, 5f, 11f, 4f, 9f, SplitMistColor, 0.85f, 1.4, 2f, -1f);   // bigger than his 9 m body: it boils OUT of him (194: from his centre)
        DiveBurst("SplitMistLow", SplitMistPuffs / 2, at - Vector3.Up * (_hoverOverFloor - 0.6f), Vector3.Up * 0.05f, 180f, 5f, 10f, 2.5f, 6f, SplitMistColor, 0.7f, 1.2, 3f, 0.2f, flat: true);
    }
    /// <summary>The floor under a point (NaN when none), for the cutscene's line.</summary>
    public float FloorYAt(Vector3 p) => FloorUnder(p);
    /// <summary>The cutscene is over: the wait at his place, the gap running (a copy's its own); the real one's reference dropped.</summary>
    public void SplitEnd()
    {
        SplitFade(1f); SetCrownGlow(_crownGlowColor, 0f);
        Position = _home;
        EnterWait(NextGap());
        if (!IsCopy) _split = null;
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the split's cutscene is over -- waits at {_home}, the next move in {_gapT:F1} s");
    }
    /// <summary>A copy made for the cutscene: as SpawnCopy, but held unseen in the Split phase for the cutscene to place and show.</summary>
    public SmsKingBooRematch SpawnCopyHeld()
    {
        if (IsCopy || GetParent() == null) return null;
        var copy = Duplicate((int)(DuplicateFlags.Scripts | DuplicateFlags.Groups | DuplicateFlags.UseInstantiation)) as SmsKingBooRematch;
        if (copy == null) { GD.PushWarning($"[SMS kingboo] {Name}: the copy could not be made"); return null; }
        CopiesMade++;
        copy.Name = $"{Name}Copy{CopiesMade}"; copy.IsCopy = true; copy.Original = this;
        GetParent().AddChild(copy);
        copy.GlobalTransform = GlobalTransform;
        copy.Hp = 1; copy.HitsTaken = 0;
        copy.ReadArena(); copy._hoverOverFloor = _hoverOverFloor; copy._opacitySolid = _opacitySolid; copy._home = copy.Position;
        if (copy._model != null) copy._model.Visible = false;                                               // unseen till the cutscene shows it (SplitFade)
        if (copy._tongue != null) { copy._tongue.Stretch = TongueStretch; copy._tongue.On = TongueStretch > 1.001f; }
        copy.ApplyTell();
        copy.SetCrownGlow(copy._crownGlowColor, 0f);
        copy.State = Phase.Split; copy.SetOpacity(0f); copy.PlayClip(BckWait);
        _copies.Add(copy);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: a COPY splits off for the cutscene ({copy.Name}; {CopiesAlive} out, {CopiesMade} made) after hit {HitsTaken}");
        EmitSignal(SignalName.CopyMade, copy);
        return copy;
    }
    /// <summary>A copy dismissed (the real one went down): its attack called off, a laugh, then it fades away and is gone. (177 ran this on a hit too; 178: a
    /// hit on a copy is the tumble and the teleport, like the real one's -- the tell is how the player knows.)</summary>
    public void DismissCopy()
    {
        if (!IsCopy || _revealing || State == Phase.Gone) return;
        if (State == Phase.Vanish || State == Phase.Arrive) { SetOpacity(_opacitySolid); }                 // mid-teleport: shown where it is for the laugh
        AbortLick(); AbortSmash(); AbortLightning(); AbortBreath(); AbortFireballs(); AbortWind(); AbortVolley(); AbortAmbush(); AbortCircle();
        _revealing = true; State = Phase.Laugh; _laughSounded = true; Position = _home; Rotation = new Vector3(0f, _yaw, 0f); _gapT = 0f;   // 191: a mock's rock undone
        PlayClip(BckLaugh);
        if (_sound != null && !string.IsNullOrEmpty(LaughSe)) _sound.Play(LaughSe, LaughDb);
        SetFlash(true); _splitFlashT = 0.3f;
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the copy is dismissed -- it laughs and fades");
    }
    /// <summary>A copy never does: the real one's entrances and the intro are his alone.</summary>
    private bool CopyMayNot(string what) { if (IsCopy) { if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: a copy does not {what}"); return true; } return false; }

    /// <summary>158: a Bomb Boo burst on him. A point off, the hurt (the hit clip, HurtTurns about himself, the flash), the Hit signal; the last takes him
    /// down. False when he cannot be hit now. 162: through a lick it breaks the lick off.</summary>
    public bool TakeHit(SmsBombBoo by)
    {
        if (!CanBeHit) return false;
        bool midLick = State == Phase.Lick, midSmash = State == Phase.Smash, midLightning = State == Phase.Lightning, midBreath = State == Phase.Breath, midFire = State == Phase.Fireballs || State == Phase.Volley || State == Phase.Circle || State == Phase.Ambush, midWind = State == Phase.Wind;   // 200/201: a volley, a circle or an ambush cut off reads as a fire
        AbortLick(); AbortSmash(); AbortLightning(); AbortBreath(); AbortFireballs(); AbortWind(); AbortVolley(); AbortAmbush(); AbortCircle();
        if (IsCopy) CopyHitsTaken++;                                                                             // 178: a copy takes the hit LIKE the real one -- the tumble, then the teleport -- but no damage
        else { Hp = Math.Max(0, Hp - 1); HitsTaken++; }
        if (IsCopy && CopyMock) { BeginMock(by); return true; }                                                  // 191: the fake LAUGHS IT OFF -- no flinch
        State = Phase.Hurt; _hurtT = 0f; _hurtYaw0 = _yaw; Position = _home;
        string hurtClip = !string.IsNullOrEmpty(HurtClip) && _anim != null && _anim.HasAnimation(HurtClip) ? HurtClip : BckHit;   // 177: the casino's tumble, else the flinch
        PlayClip(hurtClip);
        if (_anim != null && hurtClip == HurtClip) _anim.SpeedScale *= Mathf.Max(0.1f, HurtClipSpeed);
        _hurtLen = _anim != null && _anim.HasAnimation(hurtClip) ? (float)(_anim.GetAnimation(hurtClip).Length / Mathf.Max(0.01f, _anim.SpeedScale)) : 0.7f;
        _copyPending = !IsCopy && CopyEveryHits > 0 && Hp > 0 && HitsTaken % CopyEveryHits == 0 && CopiesAlive < MaxCopies;   // 177: a copy splits off as he recovers
        SetFlash(true);
        _opacitySolid = Mathf.Clamp(GhostOpacity + OpacityPerHit * (IsCopy && Original != null ? Original.HitsTaken : HitsTaken), 0.1f, 1f);   // a copy as solid as the real one (no tell there)
        SetOpacity(_opacitySolid);
        if (_sound != null && !string.IsNullOrEmpty(HurtSe)) _sound.Play(HurtSe);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: HIT by {by?.Name ?? "?"}{(midLick ? " mid-lick" : midSmash ? " mid-smash (the tell called off)" : midLightning ? " mid-lightning (the walls gone)" : midBreath ? " mid-breath (the breath cut off)" : midFire ? " mid-volley (the fireballs fizzle)" : midWind ? " mid-wind (the tornados gone)" : "")} -> {(IsCopy ? $"a COPY: no damage (its hit {CopyHitsTaken})" : $"{Hp} left")} (the hit clip, {_hurtLen:F2} s)");
        if (IsCopy) Original?.EmitSignal(SignalName.CopyHit, this); else EmitSignal(SignalName.Hit, Hp);
        if (!IsCopy && Hp == 0) GcQuietBegin("the fatal hit");                                                       // 226: the collector's one pause now, under the blast and his flash, then none through his death, the Shine and the card
        return true;
    }
    // ---- 226: THE COLLECTOR HELD OFF -- .NET's no-GC region through his death, the Shine and the card save -------------------------------------------------
    /// <summary>226: the .NET garbage collector's pauses -- 41-45 ms each, whenever its budget (about 8 MB; the port makes about 1 MB of garbage a second
    /// in play) fills -- fell into the frame of his down and the frame the card save opened on. From the fatal hit the collector is asked for a NO-GC
    /// REGION of GcQuietMegabytes: it collects once, then (the collector's own compacting collection, under the blast and his flash) holds off until the
    /// region ends -- when control is back after the card save (a second's grace), after GcQuietMaxSeconds, or when he leaves the tree -- or until the
    /// region's bytes run out (then the collector resumes on its own: the fallback for a player who dawdles before the Shine). Off = the collector as it is.</summary>
    [Export] public bool GcQuietThroughDeath = true;
    [Export(PropertyHint.Range, "16,240,8")] public int GcQuietMegabytes = 64;
    [Export(PropertyHint.Range, "10,600,5")] public float GcQuietMaxSeconds = 120f;
    private bool _gcQuiet, _gcSawGet; private float _gcQuietT, _gcBackT = -1f; private double _gcPause0, _gcPausedInside; private int _gcCount0, _gcCollectionsInside;
    public bool GcQuietOn => _gcQuiet;
    public float GcQuietSeconds => _gcQuietT;
    /// <summary>For the drives: the collector's pause time (ms) and collections inside the region (its own first collection excluded) -- live while it
    /// is on, frozen at its end.</summary>
    public double GcQuietPausedMs => _gcQuiet ? GC.GetTotalPauseDuration().TotalMilliseconds - _gcPause0 : _gcPausedInside;
    public int GcQuietCollections => _gcQuiet ? GC.CollectionCount(0) - _gcCount0 : _gcCollectionsInside;
    private void GcQuietBegin(string why)
    {
        if (!GcQuietThroughDeath || _gcQuiet || IsCopy) return;
        if (System.Runtime.GCSettings.LatencyMode == System.Runtime.GCLatencyMode.NoGCRegion) return;
        var sw = System.Diagnostics.Stopwatch.StartNew(); bool ok = false; string note = "";
        try { ok = GC.TryStartNoGCRegion((long)GcQuietMegabytes * 1048576L); }
        catch (Exception e) { note = e.GetType().Name + ": " + e.Message; }
        _gcQuiet = ok; _gcQuietT = 0f; _gcSawGet = false; _gcBackT = -1f;
        _gcPause0 = GC.GetTotalPauseDuration().TotalMilliseconds; _gcCount0 = GC.CollectionCount(0);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: {why}: the collector's NO-GC REGION {(ok ? "begun" : "REFUSED")} ({GcQuietMegabytes} MB{(note != "" ? "; " + note : "")}) in {sw.Elapsed.TotalMilliseconds:F1} ms (its own collection first); the heap {GC.GetTotalMemory(false) / 1048576.0:F1} MB");
    }
    private void GcQuietEnd(string why)
    {
        if (!_gcQuiet) return;
        _gcPausedInside = GC.GetTotalPauseDuration().TotalMilliseconds - _gcPause0; _gcCollectionsInside = GC.CollectionCount(0) - _gcCount0;
        _gcQuiet = false;
        bool still = System.Runtime.GCSettings.LatencyMode == System.Runtime.GCLatencyMode.NoGCRegion;
        if (still) { try { GC.EndNoGCRegion(); } catch (InvalidOperationException) { still = false; } }
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: {why}: the collector's no-GC region {(still ? "ended" : "had ENDED ON ITS OWN (its bytes ran out)")} after {_gcQuietT:F1} s -- {GcQuietCollections} collection(s) and {GcQuietPausedMs:F0} ms of pauses inside it; the heap {GC.GetTotalMemory(false) / 1048576.0:F1} MB");
    }
    private void GcQuietTick(float dt)
    {
        if (!_gcQuiet) return;
        _gcQuietT += dt;
        if (System.Runtime.GCSettings.LatencyMode != System.Runtime.GCLatencyMode.NoGCRegion) { GcQuietEnd("the budget passed"); return; }
        if (_gcQuietT > GcQuietMaxSeconds) { GcQuietEnd($"{GcQuietMaxSeconds:F0} s"); return; }
        var m = M(); if (m == null) return;
        if (m.SmsShineDemoRunning) { _gcSawGet = true; _gcBackT = -1f; return; }
        bool cardOpen = SmsCardSave.Current != null && SmsCardSave.Current.IsOpen;
        if (_gcSawGet && !cardOpen && m.stateOfMario != Mario.MarioState.shineGet) { if (_gcBackT < 0f) _gcBackT = 0f; _gcBackT += dt; if (_gcBackT >= 1f) GcQuietEnd("control back after the card save"); }
    }
    // ---- 191: THE MOCK -- a copy hit laughs it off ------------------------------------------------------------------------------------------------
    private float _mockT; private int _mockYawSign = 1;
    /// <summary>191, for the drives: the mocks this copy has given; the lowest its opacity dipped through the last.</summary>
    public int MocksGiven { get; private set; }
    public float MockOpacityMin { get; private set; } = 1f;
    /// <summary>The bomb burst on this copy: it laughs it off. The laugh clip and the cackle, the crown flaring its colour; the rock, the bob and the see-through run in MockTick.</summary>
    private void BeginMock(SmsBombBoo by)
    {
        State = Phase.Mock; _mockT = 0f; MocksGiven++; MockOpacityMin = _opacityShown; _mockYawSign = (_rng.NextDouble() < 0.5) ? -1 : 1;
        Position = _home; Rotation = new Vector3(0f, _yaw, 0f);
        SetFlash(false); _splitFlashT = 0f;
        PlayClip(BckLaugh);
        if (_sound != null && !string.IsNullOrEmpty(CopyMockSe)) _sound.Play(CopyMockSe, LaughDb);
        SetCrownGlow(CopyCrownColor, 1.5f);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: HIT by {by?.Name ?? "?"} -- a COPY: it LAUGHS IT OFF (no flinch; its hit {CopyHitsTaken}), see-through to {CopyMockOpacity} of its solid for {CopyMockSeconds:F1} s, then {(CopyMockTeleports ? "off to another spot" : "the wait")}");
        if (Original != null) Original.EmitSignal(SignalName.CopyHit, this);
    }
    private void MockTick(float dt)
    {
        _mockT += dt;
        float k = Mathf.Clamp(_mockT / Mathf.Max(0.05f, CopyMockSeconds), 0f, 1f), ease = 1f - k;
        // see-through: the body PHASES -- flickering between solid and CopyMockOpacity at CopyMockFlickerHz, deepest at the middle, solid again by the end (a steady dim read
        // as a dark grey blob against the black sky: white at 0.3 over black IS grey; the flicker reads as a ghost)
        float dip = MathF.Sin(MathF.PI * k) * (CopyMockFlickerHz > 0f ? 0.5f + 0.5f * MathF.Sin(_mockT * MathF.Tau * CopyMockFlickerHz - MathF.PI * 0.5f) : 1f);
        float o = _opacitySolid - (_opacitySolid - Mathf.Min(_opacitySolid, CopyMockOpacity)) * dip;
        SetOpacity(o); if (o < MockOpacityMin) MockOpacityMin = o;
        float roll = Mathf.DegToRad(CopyMockRockDeg) * MathF.Sin(_mockT * MathF.Tau * 3f) * ease * _mockYawSign;      // the mocking rock, dying out
        Rotation = new Vector3(0f, _yaw, roll);
        Position = _home + Vector3.Up * (CopyMockBob * MathF.Abs(MathF.Sin(_mockT * MathF.Tau * 2f)) * ease);          // the bob
        SetCrownGlow(CopyCrownColor, 1.5f * ease);
        _crownMat?.SetShaderParameter("opacity", Mathf.Lerp(_opacitySolid, 1f, _crownGlowK) * (o / Mathf.Max(0.01f, _opacitySolid)));   // the crown phases with the body (SetCrownGlow leaves it solid)
        if (k >= 1f) EndMock();
    }
    private void EndMock()
    {
        SetOpacity(_opacitySolid); Rotation = new Vector3(0f, _yaw, 0f); Position = _home; SetCrownGlow(_crownGlowColor, 0f);   // SetCrownGlow puts the crown's opacity back
        if (CopyMockTeleports)                                                                                          // 178's reset: off to another spot, a full gap there
        {
            _afterHit = true; _moveIsLick = false; State = Phase.Vanish; _fadeT = 0f; MovesMade++;
            if (!string.IsNullOrEmpty(TeleportClip)) PlayClip(TeleportClip);
            if (_sound != null && !string.IsNullOrEmpty(VanishSe)) _sound.Play(VanishSe);
            if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the mock is over -- vanishes to another spot");
        }
        else { EnterWait(NextGap()); if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the mock is over -- the wait, the next move in {_gapT:F1} s"); }
    }
    // ---- 192: the hits drawn ------------------------------------------------------------------------------------------------------------------------
    private MeshInstance3D _marioHitBox;
    /// <summary>192, for the drives: Mario's cylinder is drawn now.</summary>
    public bool MarioHitBoxShown => _marioHitBox != null && IsInstanceValid(_marioHitBox) && _marioHitBox.Visible;
    /// <summary>ShowHitBoxes: Mario's damage cylinder (the game's 42 u across, 130 u tall from his feet) drawn green where he stands; the walls and the breath draw their own.</summary>
    private void HitBoxTick()
    {
        if (!ShowHitBoxes) { if (_marioHitBox != null && IsInstanceValid(_marioHitBox)) _marioHitBox.Visible = false; return; }
        var m = M(); if (m == null) return;
        float r = SmsParams.Mario.DamageRadius / U, h = SmsParams.Mario.DamageHeight / U;
        if (_marioHitBox == null || !IsInstanceValid(_marioHitBox))
        {
            var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, AlbedoColor = new Color(0.2f, 1f, 0.3f, 0.3f), RenderPriority = 3 };
            _marioHitBox = new MeshInstance3D { Name = "MarioHitBox", TopLevel = true, Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = h, RadialSegments = 24, Rings = 1 }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_marioHitBox);
        }
        _marioHitBox.Visible = true;
        _marioHitBox.GlobalTransform = new Transform3D(Basis.Identity, m.GlobalPosition + Vector3.Up * (h * 0.5f));
    }
    private void AbortLick() { _lickStage = -1; _lickClipEnded = false; }
    private void AbortSmash() { SetCrownGlow(_crownGlowColor, 0f); _smashClipEnded = false; }
    private void AbortLightning() { if (_lightning != null && IsInstanceValid(_lightning)) _lightning.Cancel(); _lightning = null; SetCrownGlow(_crownGlowColor, 0f); }
    private void AbortWind() { if (_wind != null && IsInstanceValid(_wind)) _wind.Cancel(); _wind = null; SetCrownGlow(_crownGlowColor, 0f); }
    private void AbortFireballs() { if (_fireballs != null && IsInstanceValid(_fireballs)) _fireballs.Cancel(); _fireballs = null; if (_fireClipHeld && _anim != null) { _anim.Play(); _fireClipHeld = false; } SetCrownGlow(_crownGlowColor, 0f); }
    private void AbortBreath() { if (_breath != null && IsInstanceValid(_breath)) _breath.Cancel(); _breath = null; if (_breathClipHeld && _anim != null) { _anim.Play(); _breathClipHeld = false; } SetCrownGlow(_crownGlowColor, 0f); }
    private void SetCrown(Vector3 c) { _crownNow = c; _crownMat?.SetShaderParameter("crown_c0", c); }
    /// <summary>165: the crown SHINES <paramref name="color"/> by <paramref name="k"/> (0 = its own yellow, dark; over 1 = the activation flash): the shader's
    /// shine term, the colour register toward the colour, the sparkles, the faint halo.</summary>
    private void SetCrownGlow(Color color, float k)
    {
        _crownGlowColor = color; _crownShineK = Mathf.Clamp(k, 0f, 2f); _crownGlowK = Mathf.Min(_crownShineK, 1f);
        var c = new Vector3(color.R, color.G, color.B);
        SetCrown(_crownRest.Lerp(c, _crownGlowK));
        _crownMat?.SetShaderParameter("crown_glow", c); _crownMat?.SetShaderParameter("crown_glow_k", _crownShineK * CrownShine);
        _crownMat?.SetShaderParameter("opacity", Mathf.Lerp(_opacitySolid, 1f, _crownGlowK));                 // solid while it shines
        bool on = _crownGlowK > 0.02f && State != Phase.Hidden && State != Phase.Gone;
        if (_crownGlow != null)
        {
            _crownGlow.Visible = on;
            if (_crownGlowMat != null) _crownGlowMat.AlbedoColor = new Color(color.R, color.G, color.B, Mathf.Clamp(_crownGlowK * CrownGlowStrength, 0f, 1f));
        }
        if (_crownSpark != null)
        {
            if (on && !_crownSpark.Emitting) { _crownSpark.GlobalPosition = CrownFxPoint; _crownSpark.Restart(); }
            _crownSpark.Visible = on; _crownSpark.Emitting = on;
            if (_crownSparkMat != null) _crownSparkMat.Color = new Color(color.R, color.G, color.B, 1f);
        }
    }
    /// <summary>jnt_crown's place in the world (the crown's light sits there), else a little over the body joint.</summary>
    public Vector3 CrownPoint => _skel != null && _bCrown >= 0 ? _skel.GlobalTransform * _skel.GetBoneGlobalPose(_bCrown).Origin : BodyPoint + Vector3.Up * 3f;
    private void SetFlash(bool on)
    {
        _flashOn = on;
        foreach (var m in _mats)
        {
            m.SetShaderParameter("tint", on ? new Vector3(HurtFlash.R, HurtFlash.G, HurtFlash.B) : new Vector3(1f, 1f, 1f));
            m.SetShaderParameter("mask_k", on ? 1f : 0f);
        }
    }
    private void SetOpacity(float o)
    {
        _opacityShown = o;
        foreach (var m in _mats) m.SetShaderParameter("opacity", m == _teethMat && TeethHidden ? 0f : o);   // 178: a copy's tell may be its missing fangs
    }
    /// <summary>178: the copy's tell -- its crown's rest colour (every glow lerps from it) and its fangs.</summary>
    private void ApplyTell()
    {
        if (!IsCopy) return;
        if (CopyTell == Tell.Crown || CopyTell == Tell.CrownAndTeeth)
        {
            _crownRest = new Vector3(CopyCrownColor.R, CopyCrownColor.G, CopyCrownColor.B); SetCrown(_crownRest);
            _crownMat?.SetShaderParameter("crown_recolor", 1f);   // the sheet's gold strip read as brightness alone, so the colour above sets the crown's hue (BossTelesaToon.gdshaderinc)
        }
        if (TeethHidden) _teethMat.SetShaderParameter("opacity", 0f);
    }

    // ------------------------------------------------------------------------------------------------------------------ 162: the moves
    /// <summary>A move: he vanishes (the hide clip under the fade), to arrive in front of Mario for the lick, or (by RepositionChance, or when told) at another spot.</summary>
    private void StartMove(bool? lick = null)
    {
        if (M() == null) { _gapT = 0.5f; return; }
        Move pick = lick.HasValue ? (lick.Value ? Move.Lick : Move.Hop) : PickMove();
        LastMove = pick;
        if (pick == Move.Smash) { BeginSmash(); return; }
        if (pick == Move.Lightning) { BeginLightning(); return; }
        if (pick == Move.Breath) { BeginBreath(); return; }
        if (pick == Move.Fireballs) { BeginFireballs(); return; }
        if (pick == Move.Volley) { BeginVolley(); return; }
        if (pick == Move.Ambush) { BeginAmbush(); return; }
        if (pick == Move.Circle) { BeginCircle(); return; }
        if (pick == Move.Wind) { BeginWind(); return; }
        _moveIsLick = pick == Move.Lick;
        AbortLick();
        State = Phase.Vanish; _fadeT = 0f; MovesMade++;
        if (!string.IsNullOrEmpty(TeleportClip)) PlayClip(TeleportClip);
        if (_sound != null && !string.IsNullOrEmpty(VanishSe)) _sound.Play(VanishSe);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: move {MovesMade}: vanishes from {GlobalPosition} ({(_moveIsLick ? "for the LICK" : "to another spot")})");
    }
    /// <summary>The doc's near-random pick: a hop by RepositionChance, else an attack by its weight among those that are on.</summary>
    private Move PickMove()
    {
        if (IsCopy)                                                                                        // 177: a copy only THROWS -- the lava balls, the breath -- or moves off
        {
            float cf = CopyFireballs && FireballsOn ? Mathf.Max(0f, FireballWeight) : 0f, cb = CopyBreath && BreathOn ? Mathf.Max(0f, BreathWeight) : 0f, cv = CopyVolley && VolleyOn ? Mathf.Max(0f, VolleyWeight) : 0f;   // 200: the volley, when a copy may
            if (cf + cb + cv <= 0f || _rng.NextDouble() < Mathf.Max(RepositionChance, CopyRepositionChance)) return Move.Hop;   // 196: a copy mostly TELEPORTS about (0.3 before)
            float rc = (float)_rng.NextDouble() * (cf + cb + cv);
            return rc < cf ? Move.Fireballs : rc < cf + cb ? Move.Breath : Move.Volley;
        }
        if (_rng.NextDouble() < RepositionChance) return Move.Hop;
        float wl = Mathf.Max(0f, LickWeight), ws = SmashOn && SmashScene != null ? Mathf.Max(0f, SmashWeight) : 0f, wg = LightningOn ? Mathf.Max(0f, LightningWeight) : 0f, wb = BreathOn ? Mathf.Max(0f, BreathWeight) : 0f, wf = FireballsOn ? Mathf.Max(0f, FireballWeight) : 0f, ww = WindOn ? Mathf.Max(0f, WindWeight) : 0f, wv = VolleyOn ? Mathf.Max(0f, VolleyWeight) : 0f;
        float wa = AmbushOn ? Mathf.Max(0f, AmbushWeight) : 0f, wc = CircleOn && Circle == null ? Mathf.Max(0f, CircleWeight) : 0f;   // 201: one circle at a time
        float sum = wl + ws + wg + wb + wf + ww + wv + wa + wc;
        if (sum <= 0f) return Move.Lick;
        float r = (float)_rng.NextDouble() * sum;
        if (r < wl) return Move.Lick;
        if (r < wl + ws) return Move.Smash;
        if (r < wl + ws + wg) return Move.Lightning;
        if (r < wl + ws + wg + wb) return Move.Breath;
        if (r < wl + ws + wg + wb + wf) return Move.Fireballs;
        if (r < wl + ws + wg + wb + wf + ww) return Move.Wind;
        if (r < wl + ws + wg + wb + wf + ww + wv) return Move.Volley;
        if (r < wl + ws + wg + wb + wf + ww + wv + wa) return Move.Ambush;
        return Move.Circle;
    }
    /// <summary>164: the smash, where he stands: the laugh clip and its voice, the crown pulsing white through the tell, then the Boolossus over Mario.</summary>
    private void BeginSmash()
    {
        State = Phase.Smash; _smashT = 0f; _smashLaunched = false; _smashClipEnded = false; _smashLaunchedCount = 0; _smashNextAt = SmashTellSeconds; _pulsePhase = 0f; _crownFlashT = 0f; MovesMade++; SmashesMade++; Position = _home;
        var m0 = M();
        _smashBaseAngle = SmashFromKingBoo && m0 != null ? MathF.Atan2(GlobalPosition.X - m0.GlobalPosition.X, GlobalPosition.Z - m0.GlobalPosition.Z) : (float)(_rng.NextDouble() * Math.Tau);
        PlayClip(SmashClip);
        if (_sound != null && !string.IsNullOrEmpty(SmashSe)) _sound.Play(SmashSe);
        SetCrownGlow(SmashCrownColor, 0f);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: move {MovesMade}: SMASH {SmashesMade} -- the crown glows {SmashCrownColor} ({SmashCount} Boolossi from {SmashTellSeconds:F2} s, {SmashStagger:F2} s apart)");
        EmitSignal(SignalName.SmashStarted);
    }
    private void SmashTick(float dt)
    {
        _smashT += dt;
        FaceTick(dt);
        // the crown: the activation FLASH (and one at each launch), else the shine pulsing -- quicker and quicker through the tell, then at its quickest
        _crownFlashT -= dt;
        if (_smashT < CrownFlashSeconds || _crownFlashT > 0f) SetCrownGlow(SmashCrownColor, 1.8f);
        else
        {
            float hz = CrownPulseHz * Mathf.Lerp(1f, Mathf.Max(1f, CrownPulseQuicken), Mathf.Clamp(_smashT / Mathf.Max(0.1f, SmashTellSeconds), 0f, 1f));
            _pulsePhase += dt * hz * MathF.Tau;
            SetCrownGlow(SmashCrownColor, Mathf.Lerp(0.5f, 1f, 0.5f + 0.5f * MathF.Sin(_pulsePhase - MathF.PI / 2f)));
        }
        if (_smashLaunchedCount < SmashCount)
        {
            if (_smashT >= _smashNextAt)
            {
                var m = M();
                if (m == null || SmashScene == null) { EndSmash(); return; }
                int i = _smashLaunchedCount;
                float ang = _smashBaseAngle + Mathf.DegToRad(SmashSpreadDeg) * i;
                _lastApproach = new Vector3(MathF.Sin(ang), 0f, MathF.Cos(ang));
                var b = SmashScene.Instantiate<SmsBoolossus>(); b.Name = $"Boolossus{SmashesMade}_{i + 1}"; b.DebugPrint = DebugPrint;
                (GetTree().CurrentScene ?? GetParent()).AddChild(b);
                float fy = FloorUnder(m.GlobalPosition); if (float.IsNaN(fy)) fy = m.GlobalPosition.Y;
                b.Landed += hit => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; if (hit) SmashHits++; if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: a Boolossus has landed -- {(hit ? "ON Mario" : "a miss")}"); EmitSignal(SignalName.Smashed, hit); };
                b.Begin(m, fy, _lastApproach);
                _boolossus = b; _smashLaunched = true; _smashLaunchedCount++; _smashNextAt += SmashStagger;
                _crownFlashT = CrownFlashSeconds * 0.7f;
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: Boolossus {_smashLaunchedCount} of {SmashCount} dives in from {_lastApproach} at Mario {m.GlobalPosition}");
            }
        }
        else if (_smashT >= _smashNextAt - SmashStagger + SmashWatchSeconds || _smashClipEnded) EndSmash();
    }
    /// <summary>166: the lightning -- the crown red through the tell, then the three squeezes about Mario, watched to their end.</summary>
    private void BeginLightning()
    {
        State = Phase.Lightning; _lightningT = 0f; _lightningWatchT = 0f; _lightningLaunched = false; _lightningDone = false; _pulsePhase = 0f; _crownFlashT = 0f; MovesMade++; LightningsMade++; Position = _home;
        PlayClip(LightningClip);
        if (_sound != null && !string.IsNullOrEmpty(LightningSe)) _sound.Play(LightningSe);
        SetCrownGlow(LightningCrownColor, 0f);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: move {MovesMade}: LIGHTNING {LightningsMade} -- the crown glows {LightningCrownColor} (the walls in {LightningTellSeconds:F2} s)");
        EmitSignal(SignalName.LightningStarted);
    }
    private void LightningTick(float dt)
    {
        _lightningT += dt;
        FaceTick(dt);
        _crownFlashT -= dt;
        if (_lightningT < CrownFlashSeconds || _crownFlashT > 0f) SetCrownGlow(LightningCrownColor, 1.8f);
        else
        {
            float hz = CrownPulseHz * Mathf.Lerp(1f, Mathf.Max(1f, CrownPulseQuicken), Mathf.Clamp(_lightningT / Mathf.Max(0.1f, LightningTellSeconds), 0f, 1f));
            _pulsePhase += dt * hz * MathF.Tau;
            SetCrownGlow(LightningCrownColor, Mathf.Lerp(0.5f, 1f, 0.5f + 0.5f * MathF.Sin(_pulsePhase - MathF.PI / 2f)));
        }
        if (!_lightningLaunched)
        {
            if (_lightningT >= LightningTellSeconds)
            {
                var m = M();
                if (m == null) { EndLightning(); return; }
                var l = new SmsGhostLightning { Name = $"GhostLightning{LightningsMade}", DebugPrint = DebugPrint, WallLength = WallLength, WallHeight = WallHeight, WallGap = WallGap, WallSpeed = WallSpeed, SqueezeCount = SqueezeCount, WarnSeconds = WarnSeconds, SqueezePause = SqueezePause, SqueezeTurnDeg = SqueezeTurnDeg, OneShockPerAttack = OneShockPerAttack, WallHitTopK = WallHitTopK, ShowHitBox = ShowHitBoxes };   // 192
                (GetTree().CurrentScene ?? GetParent()).AddChild(l);
                float fy = FloorUnder(m.GlobalPosition); if (float.IsNaN(fy)) fy = m.GlobalPosition.Y;
                l.Squeezed += hit => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; if (hit) LightningHits++; _crownFlashT = CrownFlashSeconds * 0.7f; EmitSignal(SignalName.LightningSqueezed, hit); };
                l.Finished += () => _lightningDone = true;
                l.Begin(m, fy);
                _lightning = l; _lightningLaunched = true; _crownFlashT = CrownFlashSeconds;
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the lightning is out about Mario at {m.GlobalPosition}");
            }
        }
        else if (_lightningDone) { _lightningWatchT += dt; if (_lightningWatchT >= LightningWatchSeconds) EndLightning(); }   // the walls are done: a breath, then the wait
    }
    private float _lightningWatchT;
    private void EndLightning()
    {
        SetCrownGlow(_crownGlowColor, 0f); _lightning = null; _lightningWatchT = 0f;
        EnterWait(NextGap());
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: back to the wait after the lightning ({LightningHits} shock{(LightningHits == 1 ? "" : "s")} so far), the next move in {_gapT:F1} s");
    }
    /// <summary>167: the breath -- the crown frosty blue through the tell as the spit clip rears his head, the body turning from Mario toward the side the sweep
    /// starts from, then the thrust (the clip held at it) and the cold out of the mouth sweeping across (SmsGhostBreath) with the body turning with it;
    /// BreathWatchSeconds after the sweep, the wait. The cold rolls on on its own.</summary>
    private void BeginBreath()
    {
        State = Phase.Breath; _breathT = 0f; _breathWatchT = 0f; _breathLaunched = false; _breathSwept = false; _breathClipHeld = false; _pulsePhase = 0f; _crownFlashT = 0f; MovesMade++; BreathsMade++; Position = _home;
        FaceNow(); _breathYawCentre = _yaw;
        _breathSide = SmsGhostBreath.SideFor(SweepWay, _breathYawCentre, _rng);
        PlayClip(BreathClip);
        if (_anim != null && BreathTellSeconds > 0.05f) _anim.SpeedScale *= Mathf.Clamp(BreathHoldFrame / 60f / BreathTellSeconds, 0.2f, 4f);   // the thrust lands at the tell's end
        SetCrownGlow(BreathCrownColor, 0f);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: move {MovesMade}: BREATH {BreathsMade} -- the crown glows {BreathCrownColor} (the cold in {BreathTellSeconds:F2} s, sweeping from Mario's {(_breathSide > 0 ? "left" : "right")})");
        EmitSignal(SignalName.BreathStarted);
    }
    private void BreathTick(float dt)
    {
        _breathT += dt;
        _crownFlashT -= dt;
        if (_breathT < CrownFlashSeconds || _crownFlashT > 0f) SetCrownGlow(BreathCrownColor, 1.8f);
        else
        {
            float hz = CrownPulseHz * Mathf.Lerp(1f, Mathf.Max(1f, CrownPulseQuicken), Mathf.Clamp(_breathT / Mathf.Max(0.1f, BreathTellSeconds), 0f, 1f));
            _pulsePhase += dt * hz * MathF.Tau;
            SetCrownGlow(BreathCrownColor, Mathf.Lerp(0.5f, 1f, 0.5f + 0.5f * MathF.Sin(_pulsePhase - MathF.PI / 2f)));
        }
        if (!_breathLaunched)
        {
            // the wind-up: facing Mario through the first part of the tell, then turning toward the side the sweep starts from (the tell shows the way)
            float k = Mathf.Clamp(_breathT / Mathf.Max(0.1f, BreathTellSeconds), 0f, 1f);
            if (k < 0.4f) { FaceTick(dt); _breathYawCentre = _yaw; }
            else { float s = Mathf.SmoothStep(0f, 1f, (k - 0.4f) / 0.6f); _yaw = Mathf.LerpAngle(_breathYawCentre, _breathYawCentre - _breathSide * Mathf.DegToRad(SweepDeg) * 0.5f, s); Rotation = new Vector3(0f, _yaw, 0f); }
            if (_breathT >= BreathTellSeconds)
            {
                var m = M();
                if (m == null) { EndBreath(); return; }
                float fyAim = FloorUnder(m.GlobalPosition); if (float.IsNaN(fyAim)) fyAim = m.GlobalPosition.Y;
                BreathDownDegUsed = BreathAimFloorAt > 0f ? Mathf.RadToDeg(MathF.Atan2(Mathf.Max(0.1f, MouthPoint.Y - fyAim), BreathAimFloorAt)) : BreathDownDeg;   // 173: the floor met BreathAimFloorAt out
                var b = new SmsGhostBreath { Name = $"GhostBreath{BreathsMade}", DebugPrint = DebugPrint, SweepDeg = SweepDeg, SweepSeconds = SweepSeconds / ThrowSpeedScale, SweepWay = SweepWay, DownDeg = BreathDownDegUsed, BreathSpeed = BreathSpeed * ThrowSpeedScale, PuffSeconds = PuffSeconds, PuffSizeStart = PuffSizeStart, PuffSizeEnd = PuffSizeEnd, HitHeight = BreathHitHeight, FreezeSeconds = FreezeSeconds, OneFreezePerAttack = OneFreezePerAttack, ShowHitBox = ShowHitBoxes, FloorDrag = BreathFloorDrag, MouthWidth = BreathMouthWidth, MouthHeight = BreathMouthHeight, HitLanes = BreathHitLanes };   // 192 / 199
                (GetTree().CurrentScene ?? GetParent()).AddChild(b);
                float fy = FloorUnder(m.GlobalPosition); if (float.IsNaN(fy)) fy = m.GlobalPosition.Y;
                b.Swept += hit => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; if (hit) BreathHits++; _breathSwept = true; _crownFlashT = CrownFlashSeconds * 0.7f; EmitSignal(SignalName.Breathed, hit); };
                b.Finished += () => { if (_breath == b) _breath = null; };
                b.Begin(m, fy, () => MouthPoint, _breathYawCentre, _breathSide);
                float frame = ClipFrameNow(BreathClip);
                if (_anim != null && _anim.IsPlaying() && _anim.CurrentAnimation == BreathClip) { _anim.Pause(); _breathClipHeld = true; }   // the thrust held through the sweep
                _breath = b; _breathLaunched = true; _crownFlashT = CrownFlashSeconds;
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the cold is out of the mouth at {MouthPoint} (the clip held at frame {frame:F0})");
            }
        }
        else
        {
            var b = Breath;
            if (b != null && b.State == SmsGhostBreath.Phase.Blow) { _yaw = b.YawNow; Rotation = new Vector3(0f, _yaw, 0f); }   // the body turns with the sweep
            else if (_breathClipHeld && _anim != null) { _anim.Play(); _breathClipHeld = false; }                                   // the sweep over: the clip runs on
            if (_breathSwept) { _breathWatchT += dt; if (_breathWatchT >= BreathWatchSeconds) EndBreath(); }
            else if (b == null) EndBreath();
        }
    }
    private void EndBreath()
    {
        SetCrownGlow(_crownGlowColor, 0f); _breathWatchT = 0f;
        if (_breathClipHeld && _anim != null) { _anim.Play(); _breathClipHeld = false; }
        _breath = null;   // the cold rolls on on its own
        EnterWait(NextGap());
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: back to the wait after the breath ({BreathHits} freeze{(BreathHits == 1 ? "" : "s")} so far), the next move in {_gapT:F1} s");
    }
    /// <summary>168: the fireballs -- the crown red-orange through the tell as the spit clip rears, facing Mario, then the thrust (the clip held) and the volley
    /// out of the mouth (SmsFireballs, on its own); FireballWatchSeconds after the last ball leaves, the wait.</summary>
    private void BeginFireballs()
    {
        State = Phase.Fireballs; _fireT = 0f; _fireWatchT = 0f; _fireLaunched = false; _fireShot = false; _fireClipHeld = false; _pulsePhase = 0f; _crownFlashT = 0f; MovesMade++; FireballVolleys++; Position = _home;
        PlayClip(FireballClip);
        if (_anim != null && FireballTellSeconds > 0.05f) _anim.SpeedScale *= Mathf.Clamp(FireballHoldFrame / 60f / FireballTellSeconds, 0.2f, 4f);   // the thrust lands at the tell's end
        SetCrownGlow(FireballCrownColor, 0f);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: move {MovesMade}: FIREBALLS {FireballVolleys} -- the crown glows {FireballCrownColor} ({FireballCount} lava balls in {FireballPops} pops from {FireballTellSeconds:F2} s, {FireballPopSeconds:F2} s apart, out of the crown)");
        EmitSignal(SignalName.FireballsStarted);
    }
    private void FireballsTick(float dt)
    {
        _fireT += dt;
        FaceTick(dt);
        _crownFlashT -= dt;
        if (_fireT < CrownFlashSeconds || _crownFlashT > 0f) SetCrownGlow(FireballCrownColor, 1.8f);
        else
        {
            float hz = CrownPulseHz * Mathf.Lerp(1f, Mathf.Max(1f, CrownPulseQuicken), Mathf.Clamp(_fireT / Mathf.Max(0.1f, FireballTellSeconds), 0f, 1f));
            _pulsePhase += dt * hz * MathF.Tau;
            SetCrownGlow(FireballCrownColor, Mathf.Lerp(0.5f, 1f, 0.5f + 0.5f * MathF.Sin(_pulsePhase - MathF.PI / 2f)));
        }
        if (!_fireLaunched)
        {
            if (_fireT >= FireballTellSeconds)
            {
                var m = M();
                if (m == null) { EndFireballs(); return; }
                var f = new SmsFireballs { Name = $"Fireballs{FireballVolleys}", DebugPrint = DebugPrint, Pops = FireballPops, PerPop = FireballPerPop, PopSeconds = FireballPopSeconds / ThrowSpeedScale, LobHeight = FireballLobHeight, Scatter = FireballScatter, AimedWithin = FireballAimedWithin, RollSpeed = FireballRollSpeed * ThrowSpeedScale, Size = FireballSize, LifeSeconds = FireballSeconds, HitHeight = FireballHitHeight, DiesOnHit = FireballDiesOnHit };
                (GetTree().CurrentScene ?? GetParent()).AddChild(f);
                float fy = FloorUnder(m.GlobalPosition); if (float.IsNaN(fy)) fy = m.GlobalPosition.Y;
                Vector3 d = m.GlobalPosition - GlobalPosition; float yawTo = d.LengthSquared() > 1e-4f ? MathF.Atan2(d.X, d.Z) : _yaw;
                // 222: the fireballs live under the scene, not under him -- a copy dismissed (or him gone) while they fly is freed first, and their signals then
                // found a disposed object (ObjectDisposedException from SmsFireballs, as the volley's Boos had it): nothing to report once he is gone
                f.Shot += n => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; _fireShot = true; _crownFlashT = CrownFlashSeconds * 0.7f; EmitSignal(SignalName.FireballsShot); };
                f.Burned += () => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; FireballBurns++; EmitSignal(SignalName.FireballBurned); };
                f.Finished += () => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; int burns = f.Burns; if (_fireballs == f) _fireballs = null; EmitSignal(SignalName.FireballsDone, burns); };
                f.Begin(m, fy, () => CrownFxPoint, yawTo, _arenaCentre, _arenaRadius);                                                        // 172: out of the crown
                float frame = ClipFrameNow(FireballClip);
                if (_anim != null && _anim.IsPlaying() && _anim.CurrentAnimation == FireballClip) { _anim.Pause(); _fireClipHeld = true; }   // the thrust held through the volley
                _fireballs = f; _fireLaunched = true; _crownFlashT = CrownFlashSeconds;
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the lava balls pop out of the crown at {CrownFxPoint} (the clip held at frame {frame:F0})");
            }
        }
        else
        {
            var f = Fireballs;
            if (f != null && f.State == SmsFireballs.Phase.Volley) { }                                                                    // the clip held through the volley
            else if (_fireClipHeld && _anim != null) { _anim.Play(); _fireClipHeld = false; }                                   // the volley over: the clip runs on
            if (_fireShot) { _fireWatchT += dt; if (_fireWatchT >= FireballWatchSeconds) EndFireballs(); }
            else if (f == null) EndFireballs();
        }
    }
    private void EndFireballs()
    {
        SetCrownGlow(_crownGlowColor, 0f); _fireWatchT = 0f;
        if (_fireClipHeld && _anim != null) { _anim.Play(); _fireClipHeld = false; }
        _fireballs = null;   // the balls roll on on their own
        EnterWait(NextGap());
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: back to the wait after the fireballs ({FireballBurns} burn{(FireballBurns == 1 ? "" : "s")} so far), the next move in {_gapT:F1} s");
    }
    /// <summary>200: the Bomb Boo VOLLEY, where he stands: the crown yellow through the tell as the spit clip rears, then the thrust (the clip held at it) and
    /// VolleyCount lit Bomb Boos SPAT out of the mouth (SmsBooVolley, on its own: the lob, the marks, the blasts, the dud); VolleyWatchSeconds after the last
    /// leaves, the wait (the Boos come down on their own). The Boos are the supply's scene under the supply's parent, so they are cleared with the field.</summary>
    private void BeginVolley()
    {
        State = Phase.Volley; _volleyT = 0f; _volleyWatchT = 0f; _volleyLaunched = false; _volleySpat = false; _volleyClipHeld = false; _pulsePhase = 0f; _crownFlashT = 0f; MovesMade++; VolleysMade++; Position = _home;
        PlayClip(VolleyClip);
        if (_anim != null && VolleyTellSeconds > 0.05f) _anim.SpeedScale *= Mathf.Clamp(VolleyHoldFrame / 60f / VolleyTellSeconds, 0.2f, 4f);   // the thrust lands at the tell's end
        SetCrownGlow(VolleyCrownColor, 0f);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: move {MovesMade}: BOMB BOO VOLLEY {VolleysMade} -- the crown glows {VolleyCrownColor} ({VolleyCount} Bomb Boos from {VolleyTellSeconds:F2} s, {VolleySpacing:F2} s apart, out of the mouth{(VolleyDud ? ", the last a dud" : "")})");
        EmitSignal(SignalName.VolleyStarted);
    }
    private void VolleyTick(float dt)
    {
        _volleyT += dt;
        FaceTick(dt);
        _crownFlashT -= dt;
        if (_volleyT < CrownFlashSeconds || _crownFlashT > 0f) SetCrownGlow(VolleyCrownColor, 1.8f);
        else
        {
            float hz = CrownPulseHz * Mathf.Lerp(1f, Mathf.Max(1f, CrownPulseQuicken), Mathf.Clamp(_volleyT / Mathf.Max(0.1f, VolleyTellSeconds), 0f, 1f));
            _pulsePhase += dt * hz * MathF.Tau;
            SetCrownGlow(VolleyCrownColor, Mathf.Lerp(0.5f, 1f, 0.5f + 0.5f * MathF.Sin(_pulsePhase - MathF.PI / 2f)));
        }
        if (!_volleyLaunched)
        {
            if (_volleyT >= VolleyTellSeconds)
            {
                var m = M();
                if (m == null) { EndVolley(); return; }
                SmsBombBooSpawner supply = null;
                foreach (Node n in GetTree().CurrentScene?.FindChildren("*", "", true, false) ?? new Godot.Collections.Array<Node>()) if (n is SmsBombBooSpawner s) { supply = s; break; }
                PackedScene scene = supply?.BombBooScene ?? (ResourceLoader.Exists("res://assets/Sms/Rematch/BombBoo.tscn") ? GD.Load<PackedScene>("res://assets/Sms/Rematch/BombBoo.tscn") : null);
                Node parent = supply?.GetParent() ?? GetTree().CurrentScene ?? GetParent();
                var v = new SmsBooVolley { Name = $"BooVolley{VolleysMade}", DebugPrint = DebugPrint, Count = VolleyCount, Spacing = VolleySpacing / ThrowSpeedScale, LobHeight = VolleyLobHeight, Scatter = VolleyScatter, AimedWithin = VolleyAimedWithin, BlastRadius = VolleyBlastRadius, BlastHeight = VolleyBlastHeight, Dud = VolleyDud, DudSeconds = VolleyDudSeconds, DudMinFromMario = VolleyDudMinFromMario };
                (GetTree().CurrentScene ?? GetParent()).AddChild(v);
                float fy = FloorUnder(m.GlobalPosition); if (float.IsNaN(fy)) fy = m.GlobalPosition.Y;
                v.Spat += n => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; _volleySpat = true; _crownFlashT = CrownFlashSeconds * 0.7f; VolleyDudBoo = v.DudBoo; EmitSignal(SignalName.VolleySpat, n); };
                v.Blasted += hit => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; if (hit) VolleyHits++; EmitSignal(SignalName.VolleyBlasted, hit); };
                v.Finished += () => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; int hits = v.Hits; if (_volley == v) _volley = null; EmitSignal(SignalName.VolleyDone, hits); };
                v.Begin(m, fy, () => MouthPoint, _arenaCentre, _stoneRadius, scene, parent, DebugPrint);                                    // 229: the wall is the stone's edge (25.1 m), not the model's span (26.4)
                float frame = ClipFrameNow(VolleyClip);
                if (_anim != null && _anim.IsPlaying() && _anim.CurrentAnimation == VolleyClip) { _anim.Pause(); _volleyClipHeld = true; }   // the thrust held through the spit
                _volley = v; _volleyLaunched = true; _crownFlashT = CrownFlashSeconds;
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the Bomb Boos come out of the mouth at {MouthPoint} (the clip held at frame {frame:F0}; the supply {(supply != null ? "found" : "NOT found -- the scene loaded")})");
            }
        }
        else
        {
            var v = Volley;
            if (v != null && v.State == SmsBooVolley.Phase.Spitting) { }                                                               // the clip held through the spit
            else if (_volleyClipHeld && _anim != null) { _anim.Play(); _volleyClipHeld = false; }                                  // the spit over: the clip runs on
            if (_volleySpat) { _volleyWatchT += dt; if (_volleyWatchT >= VolleyWatchSeconds) EndVolley(); }
            else if (v == null) EndVolley();
        }
    }
    private void EndVolley()
    {
        SetCrownGlow(_crownGlowColor, 0f); _volleyWatchT = 0f;
        if (_volleyClipHeld && _anim != null) { _anim.Play(); _volleyClipHeld = false; }
        _volley = null;   // the Boos come down on their own
        EnterWait(NextGap());
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: back to the wait after the volley ({VolleyHits} hit{(VolleyHits == 1 ? "" : "s")} so far), the next move in {_gapT:F1} s");
    }
    private void AbortVolley() { if (_volley != null && IsInstanceValid(_volley)) _volley.Cancel(); _volley = null; if (_volleyClipHeld && _anim != null) { _anim.Play(); _volleyClipHeld = false; } SetCrownGlow(_crownGlowColor, 0f); }
    /// <summary>201: the AMBUSH from below -- he sinks where he stands, his glow pool hunts Mario across the stone, stops and flares, and he erupts there with a shock ring.
    /// The rise's own effects (181/182: the glow and wisps, the appear clip's leap, the floor burst, the kick, the stars) carry it; the pool is the rise's glow, moved.</summary>
    private void BeginAmbush()
    {
        State = Phase.Ambush; _ambushStage = 0; _ambushT = 0f; _ambushHuntT = 0f; _ambushRingT = -1f; _ambushHitDone = false; AmbushRingHit = false; AmbushRingRadiusNow = 0f; MovesMade++; AmbushesMade++; Position = _home;
        float fy = FloorUnder(BodyPoint); if (float.IsNaN(fy)) fy = float.IsNaN(_floorAtSpot) ? GlobalPosition.Y - _hoverOverFloor : _floorAtSpot;
        _ambushPool = new Vector3(BodyPoint.X, fy, BodyPoint.Z);
        if (!string.IsNullOrEmpty(TeleportClip)) PlayClip(TeleportClip);
        if (_sound != null && !string.IsNullOrEmpty(VanishSe)) _sound.Play(VanishSe);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: move {MovesMade}: AMBUSH {AmbushesMade} -- he sinks at {_ambushPool} ({AmbushSinkSeconds} s), the pool hunts Mario {AmbushHuntSeconds} s at {AmbushHuntSpeed} m/s (x{AmbushHuntQuicken} by the end), the tell {AmbushTellSeconds} s, the ring {AmbushRingRadius} m");
        EmitSignal(SignalName.AmbushStarted);
    }
    private void AmbushTick(float dt)
    {
        _ambushT += dt;
        var m = M(); if (m == null) { EndAmbush(); return; }
        float marioR = SmsParams.Mario.DamageRadius / 74.44f;
        switch (_ambushStage)
        {
            case 0:   // the sink: the hide clip, the fade (a hop's vanish), his poof; then the pool
            {
                float k = AmbushSinkSeconds > 0f ? Mathf.Clamp(_ambushT / AmbushSinkSeconds, 0f, 1f) : 1f;
                SetOpacity(_opacitySolid * (1f - k));
                if (k >= 1f)
                {
                    TeleportOutFx();
                    RiseFxBegin(_ambushPool); _riseGlowSizeNow = AmbushPoolSize;
                    Position = new Vector3(_ambushPool.X, _ambushPool.Y - IntroSinkDepth, _ambushPool.Z);
                    _ambushStage = 1; _ambushT = 0f; _ambushHuntT = 0f;
                    if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: under the floor -- the pool at {_ambushPool} hunts");
                }
                break;
            }
            case 1:   // the hunt: the pool slides toward Mario's feet, quicker as it goes, inside the arena; he rides under it
            {
                _ambushHuntT += dt; float hk = AmbushHuntK;
                Vector3 d = m.GlobalPosition - _ambushPool; d.Y = 0f; float dist = d.Length();
                float speed = AmbushHuntSpeed * Mathf.Lerp(1f, Mathf.Max(1f, AmbushHuntQuicken), hk), step = MathF.Min(dist, speed * dt);
                if (dist > 1e-3f) _ambushPool += d / dist * step;
                Vector3 cd = _ambushPool - _arenaCentre; cd.Y = 0f; float cl = cd.Length(), lim = Mathf.Max(3f, _arenaRadius - 2f);
                if (cl > lim) _ambushPool = new Vector3(_arenaCentre.X + cd.X / cl * lim, _ambushPool.Y, _arenaCentre.Z + cd.Z / cl * lim);
                float fy = FloorUnder(_ambushPool + Vector3.Up); if (!float.IsNaN(fy)) _ambushPool.Y = fy;
                _riseSpot = _ambushPool; _riseFloorY = _ambushPool.Y;
                if (_riseGlow != null && IsInstanceValid(_riseGlow)) _riseGlow.GlobalPosition = _ambushPool + Vector3.Up * 0.05f;
                if (_riseWisps != null && IsInstanceValid(_riseWisps)) _riseWisps.GlobalPosition = _ambushPool + Vector3.Up * 0.3f;
                Position = new Vector3(_ambushPool.X, _ambushPool.Y - IntroSinkDepth, _ambushPool.Z);
                if (hk >= 1f)
                {
                    _ambushStage = 2; _ambushT = 0f;
                    if (_sound != null && !string.IsNullOrEmpty(LaughSe)) _sound.Play(LaughSe, LaughDb - 4f, 0.8f);                           // a low chuckle under the stone
                    if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the pool STOPS at {_ambushPool} ({Flat(_ambushPool, m.GlobalPosition):F2} m from Mario) -- the tell");
                    EmitSignal(SignalName.AmbushTell);
                }
                break;
            }
            case 2:   // the tell: the pool flares
            {
                _riseGlowFlare = 1f + 0.7f * Mathf.Clamp(_ambushT / Mathf.Max(0.05f, AmbushTellSeconds), 0f, 1f);
                if (_ambushT >= AmbushTellSeconds)
                {
                    _ambushStage = 3; _ambushT = 0f; _riseGlowFlare = 1f;
                    _introTop = _ambushPool + Vector3.Up * _hoverOverFloor; _home = _introTop; _dest = _introTop; LastSpot = _introTop; _floorAtSpot = _ambushPool.Y;
                    RiseLeapTopY = float.NegativeInfinity; SetOpacity(_opacitySolid); FaceNow();
                    _riseClipPending = true; StartAppearClip();                                                                                   // 182: the clip's own leap carries him up through the floor
                    if (!_riseClipStarted) Position = new Vector3(_ambushPool.X, _ambushPool.Y - IntroSinkDepth, _ambushPool.Z);
                    if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: ERUPTS at {_ambushPool} (the rise {RiseLength:F2} s)");
                }
                break;
            }
            case 3:   // the erupt: the leap, the break (the floor burst, the stars), the shock ring racing out
            {
                if (_riseClipStarted) RiseNodeTick();
                else { float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_ambushT / Mathf.Max(0.1f, IntroRiseSeconds), 0f, 1f)); Position = new Vector3(_introTop.X, Mathf.Lerp(_ambushPool.Y - IntroSinkDepth, _introTop.Y, k), _introTop.Z); }
                FaceTick(dt);
                RiseFxTick(dt);
                if (_riseBroke && _ambushRingT < 0f)
                {
                    _ambushRingT = 0f; AmbushBreaks++;
                    Vector3 at = new Vector3(_riseSpot.X, _riseFloorY, _riseSpot.Z);
                    if (AmbushRingRadius > 0.5f) SmsSmashFx.Ring(GetTree().CurrentScene ?? GetParent(), at, 1f, AmbushRingRadius, Mathf.Max(0.1f, AmbushRingSeconds), AmbushRingColor);
                }
                if (_ambushRingT >= 0f)
                {
                    _ambushRingT += dt;
                    float r = AmbushRingRadius * Mathf.Clamp(_ambushRingT / Mathf.Max(0.05f, AmbushRingSeconds), 0f, 1f); AmbushRingRadiusNow = r;   // read on past the hit (the drives, the shots)
                    if (!_ambushHitDone)
                    {
                        Vector3 feet = m.GlobalPosition;
                        if (feet.Y - _riseFloorY < AmbushRingHeight && Flat(feet, _riseSpot) <= r + marioR && m.stateOfMario != Mario.MarioState.hurt && m.stateOfMario != Mario.MarioState.dead)
                        {
                            int hp = m.SmsHP; m.TakeDamage(new Vector3(_riseSpot.X, _riseFloorY + 1f, _riseSpot.Z), Mario.SmsDamageProfile.EnemyCommon);
                            if (m.SmsHP < hp || m.stateOfMario == Mario.MarioState.hurt) { _ambushHitDone = true; AmbushRingHit = true; AmbushHits++; if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the ring GETS Mario at {Flat(feet, _riseSpot):F2} m (r {r:F2}); hp {m.SmsHP}"); EmitSignal(SignalName.AmbushErupted, true); }
                        }
                        if (!_ambushHitDone && _ambushRingT >= AmbushRingSeconds) { _ambushHitDone = true; EmitSignal(SignalName.AmbushErupted, false); }
                    }
                }
                if (_ambushT >= RiseLength) EndAmbush();
                break;
            }
        }
    }
    private void EndAmbush()
    {
        RiseFxEnd(); _riseGlowSizeNow = RiseGlowSize; _riseGlowFlare = 1f;
        if (_ambushStage >= 2) Position = _introTop;
        SetOpacity(_opacitySolid);
        if (_ambushRingT >= 0f && !_ambushHitDone) { _ambushHitDone = true; EmitSignal(SignalName.AmbushErupted, false); }
        EnterWait(NextGap());
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: up at {GlobalPosition} after the ambush ({AmbushHits} hit{(AmbushHits == 1 ? "" : "s")} so far), the next move in {_gapT:F1} s");
        EmitSignal(SignalName.AmbushDone, AmbushHits);
    }
    private void AbortAmbush()
    {
        if (State != Phase.Ambush) return;
        RiseFxEnd(); _riseGlowSizeNow = RiseGlowSize; _riseGlowFlare = 1f; _riseClipPending = false;
        Position = _ambushStage >= 2 ? _introTop : _home; SetOpacity(_opacitySolid);
        if (_model != null) _model.Visible = true;
    }
    /// <summary>201: the BOO CIRCLE -- his laugh through the tell (the crown violet), then the ring of hotel Boos about Mario (SmsBooCircle, on its own); CircleWatchSeconds after it is up, the wait.</summary>
    private void BeginCircle()
    {
        State = Phase.Circle; _circleT = 0f; _circleWatchT = 0f; _circleLaunched = false; _pulsePhase = 0f; _crownFlashT = 0f; MovesMade++; CirclesMade++; Position = _home;
        PlayClip(CircleClipNow);
        if (_sound != null && !string.IsNullOrEmpty(LaughSe)) _sound.Play(LaughSe, LaughDb);
        SetCrownGlow(CircleCrownColor, 0f);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: move {MovesMade}: BOO CIRCLE {CirclesMade} -- the crown glows {CircleCrownColor} ({CircleCount} places, {CircleGap} empty, {CircleRadius} m about Mario in {CircleTellSeconds:F2} s; a turn every {CircleRevSeconds} s, closing to {CircleMinRadius} m by {CircleSeconds} s)");
        EmitSignal(SignalName.CircleStarted);
    }
    private void CircleTick(float dt)
    {
        _circleT += dt;
        FaceTick(dt);
        _crownFlashT -= dt;
        if (_circleT < CrownFlashSeconds || _crownFlashT > 0f) SetCrownGlow(CircleCrownColor, 1.8f);
        else
        {
            float hz = CrownPulseHz * Mathf.Lerp(1f, Mathf.Max(1f, CrownPulseQuicken), Mathf.Clamp(_circleT / Mathf.Max(0.1f, CircleTellSeconds), 0f, 1f));
            _pulsePhase += dt * hz * MathF.Tau;
            SetCrownGlow(CircleCrownColor, Mathf.Lerp(0.5f, 1f, 0.5f + 0.5f * MathF.Sin(_pulsePhase - MathF.PI / 2f)));
        }
        if (!_circleLaunched)
        {
            if (_circleT >= CircleTellSeconds)
            {
                var m = M();
                if (m == null) { EndCircle(); return; }
                var c = new SmsBooCircle { Name = $"BooCircle{CirclesMade}", DebugPrint = DebugPrint, Count = CircleCount, Gap = CircleGap, Radius = CircleRadius, MinRadius = CircleMinRadius, Height = CircleHeight, RevSeconds = CircleRevSeconds / ThrowSpeedScale, HoldSeconds = CircleHoldSeconds, Seconds = CircleSeconds, DriftSpeed = CircleDriftSpeed };
                (GetTree().CurrentScene ?? GetParent()).AddChild(c);
                float fy = FloorUnder(m.GlobalPosition); if (float.IsNaN(fy)) fy = m.GlobalPosition.Y;
                c.Hit += () => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; CircleHits++; EmitSignal(SignalName.CircleHit); };
                c.Finished += () => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; int hits = c.Hits; if (_circle == c) _circle = null; EmitSignal(SignalName.CircleDone, hits); };
                c.Begin(m, fy, _arenaCentre, _arenaRadius);
                _circle = c; _circleLaunched = true; _crownFlashT = CrownFlashSeconds;
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the Boo circle is up about {m.GlobalPosition} ({c.BoosOut} Boos)");
                EmitSignal(SignalName.CircleMade, c.BoosOut);
            }
        }
        else { _circleWatchT += dt; if (_circleWatchT >= CircleWatchSeconds) EndCircle(); }
    }
    private void EndCircle()
    {
        SetCrownGlow(_crownGlowColor, 0f); _circleWatchT = 0f;   // the ring runs on its own (kept in _circle: a hit on him fades it; one at a time)
        EnterWait(NextGap());
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: back to the wait after the circle ({CircleHits} hit{(CircleHits == 1 ? "" : "s")} so far), the next move in {_gapT:F1} s");
    }
    private void AbortCircle() { if (_circle != null && IsInstanceValid(_circle)) _circle.Cancel(); _circle = null; if (State == Phase.Circle) SetCrownGlow(_crownGlowColor, 0f); }
    /// <summary>169: the wind -- the crown pink through the tell with the laugh, then the tornados rising away from Mario (SmsGhostWind, on their own);
    /// WindWatchSeconds after they are up, the wait.</summary>
    private void BeginWind()
    {
        State = Phase.Wind; _windT = 0f; _windWatchT = 0f; _windLaunched = false; _windDone = false; _pulsePhase = 0f; _crownFlashT = 0f; MovesMade++; WindsMade++; Position = _home;
        PlayClip(WindClip);
        if (_sound != null && !string.IsNullOrEmpty(WindSe)) _sound.Play(WindSe);
        SetCrownGlow(WindCrownColor, 0f);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: move {MovesMade}: WIND {WindsMade} -- the crown glows {WindCrownColor} ({TornadoCount} tornado(s) in {WindTellSeconds:F2} s)");
        EmitSignal(SignalName.WindStarted);
    }
    private void WindTick(float dt)
    {
        _windT += dt;
        FaceTick(dt);
        _crownFlashT -= dt;
        if (_windT < CrownFlashSeconds || _crownFlashT > 0f) SetCrownGlow(WindCrownColor, 1.8f);
        else
        {
            float hz = CrownPulseHz * Mathf.Lerp(1f, Mathf.Max(1f, CrownPulseQuicken), Mathf.Clamp(_windT / Mathf.Max(0.1f, WindTellSeconds), 0f, 1f));
            _pulsePhase += dt * hz * MathF.Tau;
            SetCrownGlow(WindCrownColor, Mathf.Lerp(0.5f, 1f, 0.5f + 0.5f * MathF.Sin(_pulsePhase - MathF.PI / 2f)));
        }
        if (!_windLaunched)
        {
            if (_windT >= WindTellSeconds)
            {
                var m = M();
                if (m == null) { EndWind(); return; }
                var w = new SmsGhostWind { Name = $"GhostWind{WindsMade}", DebugPrint = DebugPrint, Count = TornadoCount, SpawnRadius = TornadoSpawnRadius, SpawnMinFromMario = TornadoMinFromMario, LifeSeconds = TornadoSeconds, Speed = TornadoSpeed, TurnDegPerSec = TornadoTurnDegPerSec, Height = TornadoHeight, Radius = TornadoRadius, FlingSpeed = FlingSpeed, FlingUpK = FlingUpK, FlingGraceSeconds = FlingGraceSeconds, Pattern = TornadoPattern, InnerRadius = TornadoInnerRadius, SpiralCycles = TornadoSpiralCycles };   // 195: the pattern
                (GetTree().CurrentScene ?? GetParent()).AddChild(w);
                float fy = FloorUnder(m.GlobalPosition); if (float.IsNaN(fy)) fy = m.GlobalPosition.Y;
                w.Spawned += n => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; _crownFlashT = CrownFlashSeconds; EmitSignal(SignalName.WindSpawned, n); };
                w.Flung += () => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; WindFlings++; _crownFlashT = CrownFlashSeconds * 0.7f; EmitSignal(SignalName.Flung); };
                w.Finished += () => { if (!IsInstanceValid(this) || IsQueuedForDeletion()) return; int fl = w.Flings; if (_wind == w) _wind = null; _windDone = true; EmitSignal(SignalName.WindDone, fl); };
                w.Begin(m, fy, _arenaCentre, _arenaRadius);
                _wind = w; _windLaunched = true;
                if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the tornados rise about the arena {_arenaCentre} ({_arenaRadius:F1})");
            }
        }
        else { _windWatchT += dt; if (_windWatchT >= WindWatchSeconds || _windDone) EndWind(); }
    }
    private void EndWind()
    {
        SetCrownGlow(_crownGlowColor, 0f); _windWatchT = 0f;
        _wind = null;   // the tornados hunt on on their own
        EnterWait(NextGap());
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: back to the wait after the wind ({WindFlings} fling{(WindFlings == 1 ? "" : "s")} so far), the next move in {_gapT:F1} s");
    }
    private void EndSmash()
    {
        SetCrownGlow(_crownGlowColor, 0f); _smashClipEnded = false;
        EnterWait(NextGap());
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: back to the wait after the smash, the next move in {_gapT:F1} s");
    }
    /// <summary>Another spot of the arena: within SpotRadius of its middle (three quarters of the floor's radius), SpotMinFromMario to SpotMaxFromMario from Mario, over floor.
    /// 196 (Jaysonn: "the fake Boos tend to blob up"): and SpotMinFromBoos from every OTHER King Boo (the real one and the copies -- where each is, or where it is going
    /// mid-teleport); 24 draws, the first that clears everything taken, else the one farthest from the others among those that clear Mario and the floor.</summary>
    private Vector3 RandomSpot()
    {
        var m = M();
        float radius = SpotRadius > 0f ? SpotRadius : _arenaRadius * 0.75f;
        var others = new List<Vector3>();
        var real = IsCopy ? Original : this;
        if (real != null && IsInstanceValid(real))
        {
            if (real != this && real.State != Phase.Gone && real.State != Phase.Hidden) others.Add(real.SpotNow);
            foreach (var c in real.Copies) if (c != null && c != this && IsInstanceValid(c) && c.State != Phase.Gone && c.State != Phase.Hidden && c.State != Phase.Laugh) others.Add(c.SpotNow);
        }
        Vector3 best = Vector3.Zero; float bestFy = float.NaN, bestRoom = -1f;
        for (int i = 0; i < 24; i++)
        {
            float a = (float)(_rng.NextDouble() * Math.Tau), r = radius * Mathf.Sqrt((float)_rng.NextDouble());
            var p = new Vector3(_arenaCentre.X + Mathf.Cos(a) * r, _arenaCentre.Y, _arenaCentre.Z + Mathf.Sin(a) * r);
            float minFrom = IsCopy ? Mathf.Max(SpotMinFromMario, CopyMinFromMario) : SpotMinFromMario, maxFrom = IsCopy ? 0f : SpotMaxFromMario;   // 177: a copy keeps away
            if (m != null && (Flat(p, m.GlobalPosition) < minFrom || (maxFrom > 0f && Flat(p, m.GlobalPosition) > maxFrom))) continue;
            float fy = FloorUnder(p); if (float.IsNaN(fy)) continue;
            float room = float.MaxValue; foreach (var o in others) room = Mathf.Min(room, Flat(p, o));
            if (room >= SpotMinFromBoos) { _floorAtSpot = fy; p.Y = fy + _hoverOverFloor; LastSpotRoom = room; return p; }
            if (room > bestRoom) { bestRoom = room; best = p; bestFy = fy; }
        }
        if (bestRoom >= 0f) { _floorAtSpot = bestFy; best.Y = bestFy + _hoverOverFloor; LastSpotRoom = bestRoom; return best; }     // nothing cleared the others: the roomiest that cleared Mario
        _floorAtSpot = FloorUnder(_home); LastSpotRoom = -1f; return _home;
    }
    /// <summary>196: where this one of him stands for the others' spot picking -- his destination mid-teleport (Vanish / Arrive), else where he is.</summary>
    public Vector3 SpotNow => State == Phase.Vanish || State == Phase.Arrive ? _dest : GlobalPosition;
    /// <summary>196, for the drives: how far the last spot picked was from the nearest other King Boo (a big number: none; -1: no spot cleared Mario and the home was kept).</summary>
    public float LastSpotRoom { get; private set; } = float.MaxValue;
    /// <summary>196, for the drives: a spot drawn as a hop would draw it (nothing moves).</summary>
    public Vector3 DriveDrawSpot() => RandomSpot();
    /// <summary>The lick's spot: LickDistance in front of Mario along his face (behind him, or beside him, when there is no floor there), LickHeight over the floor.</summary>
    private Vector3 LickSpot()
    {
        var m = M(); Vector3 mp = m.GlobalPosition;
        Vector3 f = m.SmsFacingDir(); f.Y = 0f; f = f.LengthSquared() > 1e-6f ? f.Normalized() : Vector3.Forward;
        float h = LickHeight > 0f ? LickHeight : _hoverOverFloor;
        foreach (Vector3 dir in new[] { f, -f, f.Cross(Vector3.Up), Vector3.Up.Cross(f) })
        {
            Vector3 p = mp + dir * LickDistance;
            float fy = FloorUnder(p); if (float.IsNaN(fy)) continue;
            _floorAtSpot = fy; p.Y = fy + h; return p;
        }
        float under = FloorUnder(mp); _floorAtSpot = float.IsNaN(under) ? mp.Y : under;
        return mp + f * LickDistance + Vector3.Up * (_floorAtSpot - mp.Y + h);
    }
    private void ArriveAt(Vector3 dest)
    {
        _dest = dest; _home = dest; LastSpot = dest; TeleportsMade++;
        Position = dest + Vector3.Up * TeleportInDrop;
        FaceNow();
        State = Phase.Arrive; _fadeT = 0f;
        PlayClip(BckWait);
        SetOpacity(0f);
        if (_sound != null && !string.IsNullOrEmpty(ArriveSe)) _sound.Play(ArriveSe);
        TeleportInFx();                                                                                     // 181: a flash, a poof and stars where he comes in
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: arrives at {dest} (floor {_floorAtSpot:F2}; Mario {(M() != null ? Flat(dest, M().GlobalPosition).ToString("F1") : "?")} m off)");
        EmitSignal(SignalName.Teleported);
    }
    private void BeginLick()
    {
        State = Phase.Lick; _lickStage = 0; _lickT = 0f; _stageT = 0f; _lickHit = false; _lickClipEnded = false; LicksMade++;
        PlayClip(LickClip);
        if (_anim != null) _anim.SpeedScale *= Mathf.Max(0.1f, LickClipSpeed);
        if (_sound != null && !string.IsNullOrEmpty(LickWindSe)) _sound.Play(LickWindSe);
        if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: LICK {LicksMade}: the wind-up ({LickClip} x{LickClipSpeed}; the whip live over frames {LickHitFromFrame}-{LickHitToFrame}: from {LickHitFromFrame / 60f / Mathf.Max(0.1f, LickClipSpeed):F2} s)");
        EmitSignal(SignalName.LickStarted);
    }
    private void LickTick(float dt)
    {
        _lickT += dt; _stageT += dt;
        var m = M();
        float frame = ClipFrameNow(LickClip);
        switch (_lickStage)
        {
            case 0:                                                   // the wind-up: he rears up, the tongue overhead; still turning to him
                FaceTick(dt);
                if (frame >= LickHitFromFrame)
                {
                    _lickStage = 1; _stageT = 0f;
                    if (_sound != null && !string.IsNullOrEmpty(LickSe)) _sound.Play(LickSe);
                    if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the whip is LIVE (frame {frame:F0}, {_lickT:F2} s in; Mario {(m != null ? (m.IsOnFloor() ? "on the floor" : $"in the air, {m.GlobalPosition.Y - _floorAtSpot:F2} m over the floor") : "?")})");
                }
                break;
            case 1:                                                   // the whip across the front: the hit window
                LickHitCheck(m);
                if (_lickClipEnded || frame > LickHitToFrame)
                {
                    _lickStage = 2; _stageT = 0f;
                    if (!_lickHit) LickMisses++;
                    if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the whip has passed -- {(_lickHit ? "it GOT him" : "a miss")}");
                    EmitSignal(SignalName.Licked, _lickHit);
                }
                break;
            case 2:                                                   // the clip's tail, cut at LickEndFrame
                if (_lickClipEnded || frame >= LickEndFrame) { _lickStage = 3; _stageT = 0f; PlayClip(BckWait); }
                break;
            case 3:                                                   // a breath in the wait, then off to another spot
                if (_stageT >= AfterLickSeconds) StartMove(false);
                break;
        }
        // the body kept out of the floor through the clip's plunge
        if (!float.IsNaN(_floorAtSpot) && BodyMinOverFloor > 0f)
        {
            float bodyLocal = BodyPoint.Y - GlobalPosition.Y;
            float lift = Mathf.Max(0f, _floorAtSpot + BodyMinOverFloor - (_home.Y + bodyLocal));
            Position = _home + Vector3.Up * lift;
        }
    }
    /// <summary>The whip on Mario, once per lick: any tongue joint, or the point between two, inside his damage cylinder (42 u across, 130 u tall from his
    /// feet) grown by LickThickness -- the plain hit: Mario.TakeDamage with the common enemy profile, the knockdown, from the tongue point that got him.</summary>
    private void LickHitCheck(Mario m)
    {
        if (_lickHit || m == null || _tongue == null || !_tongue.PointsValid) return;
        Vector3 feet = m.GlobalPosition;
        float r = SmsParams.Mario.DamageRadius / U + LickThickness, bottom = feet.Y - LickThickness, top = feet.Y + SmsParams.Mario.DamageHeight / U + LickThickness;
        var pts = _tongue.Points;
        for (int i = 0; i < pts.Length; i++)
        {
            if (In(pts[i])) { Land(pts[i]); return; }
            if (i + 1 < pts.Length) { Vector3 mid = (pts[i] + pts[i + 1]) * 0.5f; if (In(mid)) { Land(mid); return; } }
        }
        bool In(Vector3 p) => p.Y >= bottom && p.Y <= top && Flat(p, feet) <= r;
        void Land(Vector3 p)
        {
            _lickHit = true; LickHits++;
            m.TakeDamage(p, Mario.SmsDamageProfile.EnemyCommon);
            if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the whip GETS Mario at {p} (Mario at {feet}; {p.Y - feet.Y:F2} m over his feet; hp {m.SmsHP})");
        }
    }
    private void FaceNow()
    {
        var m = M(); if (m == null) return;
        Vector3 d = m.GlobalPosition - GlobalPosition; d.Y = 0f;
        if (d.LengthSquared() > 1e-4f) { _yaw = MathF.Atan2(d.X, d.Z); Rotation = new Vector3(0f, _yaw, 0f); }
    }
    private void FaceTick(float dt)
    {
        if (!FaceMario) return;
        var m = M(); if (m == null) return;
        Vector3 d = m.GlobalPosition - GlobalPosition; d.Y = 0f;
        if (d.LengthSquared() > 1e-4f)
        {
            float want = MathF.Atan2(d.X, d.Z);     // +Z forward: the model faces +Z at yaw 0 (the casino places him at rotation 180 to face its camera)
            _yaw = Mathf.LerpAngle(_yaw, want, Mathf.Clamp(TurnSpeed * dt, 0f, 1f));
            Rotation = new Vector3(0f, _yaw, 0f);
        }
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) return;
        if (!IsCopy) { HitBoxTick(); FinalSkyTick((float)delta); PeaceSkyTick((float)delta); GcQuietTick((float)delta); }                                      // 226: the collector held off through his death and the Shine                                                                                          // 192: Mario's cylinder drawn (ShowHitBoxes); 208b: the final sky's fade
        if (AnyFxAlive) FxTick((float)delta);                                                                                                                   // 180 / 181: the flashes, the ring and the rise glow run on, hidden or not
        if (State == Phase.Gone) GoneTick((float)delta);                                                                                                       // 183: the Shine after the burst
        if (State == Phase.Hidden) return;
        float dt = (float)delta;
        if (State == Phase.IntroRise || State == Phase.IntroLaugh || State == Phase.IntroSink) { IntroTick(dt); if (Blink) BlinkTick(dt); PlaceFx(); return; }   // 174
        if (State == Phase.Appear && _spawnRise)                                                                                                       // 174: up out of the floor at the spawn
        {
            _introT += dt;
            RiseClipWhenDue();
            if (_riseClipStarted) RiseNodeTick();                                                                                       // 182: the clip's own leap is the rise
            else if (!_riseClipPending)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_introT / Mathf.Max(0.1f, SpawnRiseSeconds), 0f, 1f));                  // 174's lerp: no appear clip
                Position = new Vector3(_introTop.X, Mathf.Lerp(_introFloorY - IntroSinkDepth, _introTop.Y, k), _introTop.Z);
            }
            RiseFxTick(dt);
            if (_introT >= (_riseClipStarted ? _riseLen : SpawnRiseSeconds)) { _spawnRise = false; Position = _home; RiseFxEnd(); }
        }
        // the long laugh with the laugh clip (the casino boss plays it two bck frames in: a thirtieth of a second, dropped here)
        if (State == Phase.Laugh && !_laughSounded)
        {
            _laughSounded = true; LaughPlayed = true;
            if (_sound != null && !string.IsNullOrEmpty(LaughSe)) _sound.Play(LaughSe, LaughDb);
            else if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: no sound node for the laugh");
        }
        if (_splitFlashT > 0f) { _splitFlashT -= dt; if (_splitFlashT <= 0f && State != Phase.Hurt) SetFlash(false); }   // 177: the split's / the reveal's flash
        if (State == Phase.Hurt)
        {
            _hurtT += dt;
            float k = _hurtLen > 0f ? Mathf.Clamp(_hurtT / _hurtLen, 0f, 1f) : 1f;
            if (HurtTurns != 0f) Rotation = new Vector3(0f, _hurtYaw0 + HurtTurns * MathF.Tau * Mathf.SmoothStep(0f, 1f, k), 0f);   // the flinch's turn about himself (the tumble clip has its own)
            bool flash = ((int)(_hurtT * 15f) & 1) == 0 && k < 0.8f;
            if (flash != _flashOn) SetFlash(flash);
        }
        if (State == Phase.Wait)
        {
            _hoverT += dt;
            if (HoverBob > 0f && HoverBobSeconds > 0f) Position = _home + Vector3.Up * (MathF.Sin(_hoverT / HoverBobSeconds * MathF.Tau) * HoverBob);
            if (_hoverT > 0.3f && _skel != null && _bBody >= 0) { float lift = BodyPoint.Y - GlobalPosition.Y; WaitBodyLift = float.IsNaN(WaitBodyLift) ? lift : Mathf.Lerp(WaitBodyLift, lift, 0.05f); }   // 194: the wait pose's body height over the node, for the split's effects (past the clip blend)
            FaceTick(dt);
            if (AttacksOn && _gapT > 0f) { _gapT -= dt; if (_gapT <= 0f) StartMove(); }
        }
        else if (State == Phase.Split) { if (_opacityShown > 0.02f) FaceTick(dt); }                                                   // 188: the cutscene places him; he keeps his eyes on Mario
        else if (State == Phase.Vanish)
        {
            _fadeT += dt;
            float outSec = _revealing ? CopyFadeSeconds : TeleportOutSeconds;                               // 177: a found-out copy fades slower
            float k = outSec > 0f ? Mathf.Clamp(_fadeT / outSec, 0f, 1f) : 1f;
            SetOpacity(_opacitySolid * (1f - k));
            if (k >= 1f)
            {
                TeleportOutFx();                                                                           // 181: the poof and the stars where he is the moment he is gone (the hide clip has hopped him up by then)
                if (_revealing)                                                                            // 177: the found-out copy is gone
                {
                    State = Phase.Gone; if (_model != null) _model.Visible = false; PlaceFx();
                    Original?._copies.Remove(this);
                    if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: the copy is gone");
                    QueueFree(); return;
                }
                ArriveAt(_moveIsLick ? LickSpot() : RandomSpot());
            }
        }
        else if (State == Phase.Arrive)
        {
            _fadeT += dt;
            float k = TeleportInSeconds > 0f ? Mathf.Clamp(_fadeT / TeleportInSeconds, 0f, 1f) : 1f;
            SetOpacity(_opacitySolid * k);
            Position = _dest + Vector3.Up * (TeleportInDrop * (1f - k) * (1f - k));
            FaceTick(dt);
            if (k >= 1f)
            {
                Position = _dest;
                if (_moveIsLick) BeginLick();
                else { EnterWait(NextGap() * (_afterHit ? 1f : 0.5f)); _afterHit = false; if (DebugPrint) GD.Print($"[SMS kingboo] {Name}: waits at the new spot, the next move in {_gapT:F1} s"); }   // 178: the full gap after a hit's teleport
            }
        }
        else if (State == Phase.Lick) LickTick(dt);
        else if (State == Phase.Smash) SmashTick(dt);
        else if (State == Phase.Lightning) LightningTick(dt);
        else if (State == Phase.Breath) BreathTick(dt);
        else if (State == Phase.Fireballs) FireballsTick(dt);
        else if (State == Phase.Volley) VolleyTick(dt);                                                                               // 200
        else if (State == Phase.Ambush) AmbushTick(dt);                                                                               // 201
        else if (State == Phase.Circle) CircleTick(dt);                                                                               // 201
        else if (State == Phase.Wind) WindTick(dt);
        else if (State == Phase.Down) DeathTick(dt);                                                                                  // 183
        else if (State == Phase.Mock) MockTick(dt);                                                                                   // 191
        if (Blink) BlinkTick(dt);
        PlaceFx();
    }

    private void BlinkTick(float dt)
    {
        _blinkT += dt * 30f;                                     // the btp clock: 30 frames a second
        int f = (int)_blinkT % 150; int eye = 0;
        for (int i = 0; i < BlinkEnds.Length; i++) if (f < BlinkEnds[i]) { eye = BlinkEyes[i]; break; }
        ApplyEye(eye);
    }
    private void ApplyEye(int eye)
    {
        if (eye == _eyeNow || _bodyMat == null) return;
        Texture2D t = eye switch { 1 => EyeHalf, 2 => EyeClose, _ => EyeOpen } ?? EyeOpen;
        if (t == null) return;
        _eyeNow = eye;
        _bodyMat.SetShaderParameter("base_tex", t);
    }
    private void PlaceFx()
    {
        bool show = State != Phase.Hidden && State != Phase.Gone && _model != null && _model.Visible && _opacityShown > 0.02f;
        Vector3 at = BodyPoint;
        if (_chika != null) { bool on = show && Twinkle; _chika.Visible = on; _chika.Emitting = on; if (on) _chika.GlobalPosition = at; }
        if (_crownGlow != null && _crownGlow.Visible) { if (!show) _crownGlow.Visible = false; else _crownGlow.GlobalPosition = CrownFxPoint; }
        if (_crownSpark != null && _crownSpark.Visible) { if (!show) { _crownSpark.Visible = false; _crownSpark.Emitting = false; } else _crownSpark.GlobalPosition = CrownFxPoint; }
        if (_glow != null)
        {
            _glow.Visible = show && Glow > 0f;
            if (_glow.Visible)
            {
                _glow.GlobalPosition = at;
                if (_glow is MeshInstance3D gm && gm.GetActiveMaterial(0) is ShaderMaterial gsm) gsm.SetShaderParameter("strength", Glow * _opacityShown * _glowBoost);   // 180: x the dive's swell
            }
        }
    }
}
