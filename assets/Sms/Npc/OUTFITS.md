# NPC outfits -- the runbook

What a Pianta or Toad can wear, what mixes with what, which colours each piece comes in, and how to add your own.
Everything here is read off the shipped files: `Profiles/Outfit*.tres` (the wardrobes), `Parts/**/*.tscn` (the pieces),
`SmsNpc.cs` (the rules). The pieces are the game's own (`NpcInitData.cpp`'s part tables, the disc's models, its colour tables);
in Sunshine a stage's placement word picks them per NPC -- here the inspector does.

## 1. The 30-second version

Select an NPC (`Pianta.tscn`, `PiantaFemale.tscn`, `Toad.tscn` instance) and open the inspector:

| Where | What |
|---|---|
| **Outfit -> Outfit** | the wardrobe (`Profiles/OutfitPianta.tres` etc.). Swap it for another catalog and the slot dropdowns below change with it. |
| **Outfit -> Outfit Slots** | one dropdown per slot: `None` / `Random` / each piece. A chosen piece shows on the model in the editor at once; `Random` shows nothing until play. |
| **Outfit -> Extra Parts** | any part scenes, always worn on top of the slots (things held, badges, your own models). |
| **Outfit -> Random Outfit** | ON = every slot left at `None` is rolled at play like a `Random` one. OFF (default) = the game: only what you chose. |
| **Outfit -> Random Outfit Density** | scales every roll: `0.5` = the catalog's odds, `1.0` = double, `0` = nothing. |
| **Outfit -> Part Color Indices** | `(x, y, z)` = the colour row for hats / moustache-collar-flower / glasses. `-1` = random. |
| **Who -> Color Index** | the BODY colour (skin / cap), separate from the parts. `-1` = random. |

Saved per instance (`OutfitHat = 3` and friends in the scene file). The revert arrow beside a dropdown puts it back to `None`.

## 2. What mixes with what

The rule is the slot: **one piece per slot, any combination across slots.** A slot holds the pieces that would overlap on the
model (every hat sits on the head joint), so two hats cannot happen through the dropdowns. Everything else combines freely.

### Pianta (male) -- `OutfitPianta.tres`

| Slot (inspector name) | Pieces | Joint | Random odds (at density 0.5) |
|---|---|---|---|
| Hat | Hat A, Hat B, Hat D, Hat E, Hat F, Hat G | `kubi` (head) | 60 % |
| Moustache | Mustache A | `kubi` | 35 % |
| Glasses | Glasses A, Glasses B | `kubi` | 25 % |
| Neck | Lapel A, Tie B | `body_jnt` (chest) | 30 % |
| Hand | Luggage | `body_jnt` | 10 % |

Any hat + the moustache + either glasses + a collar or a tie + the luggage all fit together: 7 x 2 x 3 x 3 x 2 = **252 outfits**
counting "nothing" in each slot (before colours).

### Pianta (female) -- `OutfitPiantaFemale.tres`

| Slot | Pieces | Joint | Odds |
|---|---|---|---|
| Flower | Flower | `yashi_jnt` (the palm on her head) | 50 % |
| Hat | Hat A | `kubi` | 30 % |
| Glasses | Glasses B | `kubi` | 20 % |
| Arrows | Arrow L + Arrow R (always the pair -- `Together`) | `handR_jnt` | 10 % |

Flower + hat + glasses + arrows all combine: **16 outfits** before colours. Her pieces are her own models (the hat and glasses
are shaped for her head: `hwa_model`, `gwb_model`) -- the male's `Parts/Pianta/*` scenes are a different set.

### Toad -- `OutfitToad.tres`

| Slot | Pieces | Joint | Odds |
|---|---|---|---|
| Glasses | Sunglasses | `kubi` | 30 % |

### Across species

A part scene is just a model hung on a named joint, so any piece goes on any NPC **whose skeleton has that joint**: `kubi` exists
on all three (a Toad's sunglasses on a Pianta works -- verified in the pass-88 drive), `body_jnt` / `handR_jnt` on the Piantas,
`yashi_jnt` only on the female. A missing joint does not error: the piece sits at the model's root, which is wrong but visible.
Pieces are authored in their joint's space, so there is no offset to tune -- a piece either fits its joint or it is the wrong model.

## 3. The pieces

| Piece | Scene | Model (disc) | Colour channel | Colour rows | Notes |
|---|---|---|---|---|---|
| Hat A | `Parts/Pianta/HatA.tscn` | `hata_model` | 0 (x) | 7 | straw panama; crown (`_boushi_mat`) + band (`_obi_mat`) recolour together |
| Hat B | `Parts/Pianta/HatB.tscn` | `hatb_model` | 0 | 3 | cut-out shape (alpha texture); crown + band |
| Hat D | `Parts/Pianta/HatD.tscn` | `hatd_model` | 0 | 2 | crown has its own colour texture; only the band recolours |
| Hat E | `Parts/Pianta/HatE.tscn` | `hate_model` | 0 | 2 | striped two-tone (stripe colours swap between the rows) |
| Hat F | `Parts/Pianta/HatF.tscn` | `hatf_model` | 0 | 2 | striped two-tone |
| Hat G | `Parts/Pianta/HatG.tscn` | `hatg_model` | 0 | 5 | two-tone pattern |
| Mustache A | `Parts/Pianta/MustacheA.tscn` | `higea_model` | 1 (y) | 4 | plain colour |
| Glasses A | `Parts/Pianta/GlassesA.tscn` | `glassesa_model` | 2 (z) | -- | translucent lenses, no colour change |
| Glasses B | `Parts/Pianta/GlassesB.tscn` | `glassesb_model` | 2 | 3 | translucent, tinted |
| Lapel A | `Parts/Pianta/LapelA.tscn` | `eria_model` | 1 | 2 | sailor collar, two-tone |
| Tie B | `Parts/Pianta/TieB.tscn` | `tieb_model` | 0 | 1 | one colourway |
| Luggage | `Parts/Pianta/Luggage.tscn` | `nimotsu_model` | 0 | -- | textured (mushroom sticker), no colour change; rides `body_jnt` |
| Flower | `Parts/PiantaFemale/Flower.tscn` | `flower_model` | 1 | 3 | petals (`_hana_mat`) + centre (`_naka_mat`) |
| Hat A (W) | `Parts/PiantaFemale/HatA.tscn` | `hwa_model` | 0 | 4 | her panama; crown + band |
| Glasses B (W) | `Parts/PiantaFemale/GlassesB.tscn` | `gwb_model` | 2 | 3 | translucent, tinted |
| Arrow L / R | `Parts/PiantaFemale/ArrowL.tscn`, `ArrowR.tscn` | `arrowl_model`, `arrowr_model` | -- | -- | held signs, no colour change |
| Sunglasses | `Parts/Toad/Sunglasses.tscn` | `kinopio_sunmegane` | -- | -- | translucent, no colour change |

Dropdown labels come from the scene file names (`HatA.tscn` -> "Hat A"), so a piece you add is named by its file.

## 4. Colours

Two independent choices colour an NPC: **Color Index** (the body) and **Part Color Indices** (the pieces, one index per channel).
Values are the game's GX register colours (S10: a channel may exceed 255 -- it brightens the texture under it); the hex below is
clamped for reading, `*` = brighter than the hex shows.

### Body -- `Who -> Color Index`

| Species | Rows | Colours (index: hex) |
|---|---|---|
| Pianta (male), `_hand_mat` | 10 | 0 `#64ffff`* 1 `#7878ff`* 2 `#ffff00`* 3 `#c84600` 4 `#ff82ff`* 5 `#ffff00`* 6 `#ffffff`* 7 `#ff8c00`* 8 `#c8ffff`* 9 `#fffa64`* |
| Pianta (female), `_hand_mat` | 6 | 0 `#ff64c8`* 1 `#ff9600`* 2 `#ffff00`* 3 `#ffff00`* 4 `#ff2800`* 5 `#ffc8ff`* |
| Toad, `_mat_cap` (spots stay white) | 5 | 0 `#1e1ec8` 1 `#e60000` 2 `#1e781e` 3 `#fadc1e` 4 `#9600c8` -- the vest is `#fa9600` on all five; its second register takes the cap colour |

### Pieces -- `Outfit -> Part Color Indices`

`x` colours every channel-0 piece worn (all hats, Tie B), `y` every channel-1 piece (moustache, lapel, the flower), `z` the glasses.
**One index per channel is shared by everything on that channel** -- that is the game's rule (three indices per placement), and the
tables are designed in rows: a hat's crown and band are row N of their two tables together. An index past a table's end wraps
(`x = 5` on a 2-row hat = row 1). `-1` draws a random row from the longest table among the pieces actually worn on that channel.

| Piece | Register | Rows (index: hex) |
|---|---|---|
| Hat A crown | c1 / c2 | 0 `#c8c878`/`#a08232` 1 `#464646`/`#464646` 2 `#c8c896`/`#c8c896` 3 `#64c8c8`/`#3278a0` 4 `#001e96`/`#001e96` 5 `#c8dc78`/`#008232` 6 `#8c3200`/`#8c3200` |
| Hat A band | c0 | 0 `#640000` 1 `#000000` 2 `#000000` 3 `#0064ff`* 4 `#00c896` 5 `#008282` 6 `#640000` |
| Hat B crown / band | mat / c0 | 0 `#ffff3c`/`#644632` 1 `#ff8200`/`#644632` 2 `#ffff00`/`#0064ff` |
| Hat D band | c0 | 0 `#0064e6` 1 `#ffffff`* |
| Hat E stripes | c1 / c2 | 0 `#004696`/`#e6e6d2` 1 `#e6e6d2`/`#004696` |
| Hat F stripes | c1 / c2 | 0 `#329682`/`#e6e6d2` 1 `#3c2800`/`#a0963c` |
| Hat G pattern | c1 / c2 | 0 `#003278`/`#003278` 1 `#647800`/`#647800` 2 `#aa7800`/`#aa7800` 3 `#0064ff`/`#00b4ff` 4 `#8c3200`/`#c87800` |
| Tie B | c1 / c2 | 0 `#640000`/`#968200` |
| Mustache A | c0 | 0 `#000000` 1 `#ffff96` 2 `#640000` 3 `#ffc800` |
| Lapel A | c1 / c2 | 0 `#004696`/`#ffffe6` 1 `#ffffe6`/`#004696` |
| Flower petals / centre | mat / c0 | 0 `#dc2878` 1 `#dc2800` 2 `#c8dc00` (centre `#ffff00` on all three) |
| Glasses B (M) tint | mat | 0 `#ffe632` 1 `#ffffff` 2 `#000000` |
| Hat A (W) crown | c1 / c2 | 0 `#c8c878`/`#a08232` 1 `#a00000`/`#a00000` 2 `#be9678`/`#784632` 3 `#c8dcdc`/`#64aac8` |
| Hat A (W) band | c0 | 0 `#ff6464`* 1 `#ff6464`* 2 `#640000` 3 `#ffffff`* |
| Glasses B (W) tint | mat | 0 `#960000` 1 `#00c800` 2 `#c8c800` |

Body and piece colours are independent in the game too, so any skin goes with any hat. The pieces are lit like the body (the
scene's `DirectionalLight3D` + the toon ramp), so a shade reads differently in your level's light than in this table.

## 5. Random outfits -- what the dice do

Per slot, at play, when the slot is `Random` (or `None` with **Random Outfit** on):

    chance = min(odds x density / 0.5, 1)        -- then one piece of the slot at random (all of them for a Together slot)

| Density | Hat | Moustache | Glasses | Neck | Luggage | W Flower | W Hat | W Glasses | W Arrows | Toad glasses |
|---|---|---|---|---|---|---|---|---|---|---|
| 0.25 | 30 % | 17.5 % | 12.5 % | 15 % | 5 % | 25 % | 15 % | 10 % | 5 % | 15 % |
| **0.5** | **60 %** | **35 %** | **25 %** | **30 %** | **10 %** | **50 %** | **30 %** | **20 %** | **10 %** | **30 %** |
| 1.0 | 100 % | 70 % | 50 % | 60 % | 20 % | 100 % | 60 % | 40 % | 20 % | 60 % |

Rolls are per instance, fresh on every scene load (a death's reload re-rolls). `Part Color Indices` at `-1` roll too.
To freeze a look you like: pick it in the dropdowns and set the three indices.

## 6. Rules and gotchas

- **A hand-placed piece claims its slot.** Drop a part scene under the NPC's `Parts` node and that slot is left out of the roll
  (so `Random Outfit` never gives it a second hat). Hand-placed pieces are coloured like the rest.
- **Extra Parts do NOT claim a slot.** A hat added as an extra plus a `Random` Hat slot (or `Random Outfit` on) can give two hats.
  Use Extra Parts for pieces that have no slot (held things, badges); for a hat, use the slot.
- **The editor preview shows chosen pieces only**, with the colour indices as set (`-1` previews as row 0). `Random` shows nothing.
  The preview nodes are never saved into your scene.
- **Random is a deviation.** The game's parts are stage data; `Random` / `Random Outfit` are this port's addition (your ask, pass 85).
  Leave `Random Outfit` off and every slot explicit for a game-exact placement.
- **Three indices, not one per piece.** A Pianta cannot wear a row-3 hat with a row-5 tie; the hat and the tie share `x`. If you need
  that, it is a different piece (a scene with its own baked colour), not a different index.
- **Odds live in the catalog**, not the NPC: edit `Profiles/OutfitPianta.tres` -> the slot -> `Odds`; `Density` on the NPC scales them all.

## 7. Adding your own

**A new piece in an existing slot** (the shortest path):
1. Make a scene whose root is a `Node3D` with the `SmsNpcPart` script; set `Joint` (`kubi` head, `body_jnt` chest, `handR_jnt` right
   hand, `yashi_jnt` the W's palm) and `Color Channel` (0 hats, 1 moustache/collar/flower, 2 glasses). Put the model under it,
   positioned as it should sit AT the joint (identity = the joint's origin and axes).
2. Optional recolouring: add `Mat0..Mat2` (`SmsNpcPartMaterial`): `Material Name` = the model's surface material name, a
   `ShaderMaterial` on `NpcPart.gdshader` (or `NpcPartCutout` for alpha-cut, `NpcPartBlend` for translucent), `Color Kind`
   (0 = `mat_color`, 1 = `c0`, 2 = `c1` + `c2`) and the row tables. Without materials the model's own import materials are used, uncoloured.
3. Open the catalog (`Profiles/OutfitPianta.tres`), the slot, append your scene to `Parts`. It appears in the dropdown at once, named
   from the file (`CrownA.tscn` -> "Crown A").

**A new slot**: in the catalog add an `SmsNpcOutfitSlot` to `Slots` with a `Name` (the inspector property becomes `Outfit<Name>`), its
`Parts`, `Odds`, and `Together` if the pieces are a set. Every NPC on that catalog gets the dropdown.

**A new wardrobe**: duplicate a catalog, change what you like, assign it to the NPC's `Outfit`. Your pieces in the slots of a
copy = a town's own fashion while the stock Piantas keep the game's.

**Something held**: an `SmsNpcPart` scene on `handR_jnt`, in `Extra Parts` (always) or in a slot of its own (optional / random).

## 7b. The other per-instance switches (not outfits, but on the same inspector)

| Group | Field | What it does |
|---|---|---|
| Who | `Idle` | Wait / WaitA / WaitB / Talking / Angry / Dance / Sit / SitTalk -- the standing clip |
| Talk | `Lines`, `CanTalk` | the balloon's pages (Enter = a new line, up to three a page); empty or off = no cursor. A page ending in `<yesno>` asks: when its text is in, the game's yes/no box (`me_4`) replaces the continue mark -- stick or D-pad up/down move the highlight, A or B confirm (B does not cancel). `<s0=Sure>` / `<s1=No way>` name the two options yourself (16 chars each, the first one met is the one highlighted). The box shows on the page that carries the tags; one answer per talk, reset when the next talk starts. |
| Talk | `On Yes`, `On No` | what the answer does, on the NPC: None / Stop Worrying / Start Worrying. `Last Choice` (0 = the first option, 1 = the second, -1 = none yet) and the `TalkChoiceMade(choice)` signal carry it to your own scripts -- in the game the answer goes to the stage script, so anything beyond the Toad's worry is yours to wire. |
| Who | `Worried` | the Toad's worried kind (the game's placement flag 0x100): wait / sit / run / walk play their fidgety `ororo` variants, the talk its `worry` clips, the blink `kinopio_twait2` and the talking face `kinopio_tworry` (the same face a dirty Toad wears). Flip it on a standing Toad and it re-plays its clip in the other variant. The swap tables are on the profile (`Worried Clips` / `Worried Patterns`); a Pianta's are empty, so the switch does nothing there. `Toad.tscn` ships Worried with a two-page question (`On Yes` = Stop Worrying) as the test: yes calms him, no leaves him shaking. |
| Take | `Can Be Taken` | B lifts it instead of talking (the game's stage-script flag); carried, put down or pitched |
| Throw | `Throws Mario`, `Throw Speed`, `Throw Angle Deg` | the Pianta Village launcher: when the talk ends it hurls Mario up and over its own head, to its BACK, at the speed (units a tick) and angle. It takes the talk only within 200 units (not 400). Stand it with its back to the destination. |
| Pollution | `Pollution` | 0..1: placed already goop-covered (dirty wait, no talk, no turning, the head straight) until the hose washes it clean (125 hits at the Pianta's 0.008) |
| Pollution | `Thank You Coin` | a scene to pop out 40 frames after the talk that follows a wash (the game's TNpcCoin). Empty = none. |
| Burning | `Burning`, `Burn Strength` | the Delfino Plaza / Pianta Village kind: on fire from the start -- smoke and flames on its **luggage**: a burning Pianta is given the Luggage part automatically (every one of the game's 8 burning placements carries it; `Profile.Burn With Luggage` turns that off and puts the fire on the palm on its head instead, which no shipped stage does). Tick `Burning` in the editor and the bag appears in the preview. Every hose hit on it flashes the game's water-hit mark (the yellow splat) on the fire -- the backpack (`Profile.Water Hit Mark At` = Fire, the default; Drop = where the water landed on its hit sphere; Feet = the game's own spot, its position on the ground). If it wanders it RUNS the route (1.5 x the run speed) without pausing at the nodes. No talk, no mad, no sinking while it burns. The hose puts it out: 500 hits at full strength (0.002 a hit), the flames shrinking as it goes; then it stands in the happy pose until you talk to it, and that talk drops the `Thank You Coin`. `Burn Strength` is how much is left to put out (1 = the game's start). Only a male Pianta can burn -- on a female or a Toad the flag warns and does nothing (`Profile.CanBurn`). Touching a burning Pianta does not hurt Mario (the game's NPCs never send a burn). |
| Burning | `Burning Sound`, `Fire Fighting Sound` | the two sound slots: the loop while it burns, and the per-hit sizzle under the hose. Your WAVs. |
| Wander | `Wander`, `Graph`, `Wander Radius Units` | walk a route of Marker3Ds, or random points in the radius |

Any NPC standing on painted goop sinks into it by itself (220 units a Pianta, 70 a Toad) and comes out dirty when the goop under
it is cleaned; `Profile.SinksInPollution` turns that off for a profile.

## 7c. Per-profile looks and effects (pass 95: `Profiles/*.tres`, the "Leaves" and "Effects" groups)

These sit on the PROFILE, not the instance -- change them on a duplicated profile for a new kind of Pianta or Toad.

| Group | Knob | What it does |
|---|---|---|
| Leaves | `Leaf Material`, `Leaf Texture` | the Pianta's leaf fringe (`_leafback_mat`) and the texture the game swaps in for the model's dummy. Every stage of Sunshine ships its own `I_mom_mino_rgba` / `I_mow_mino_rgba`; Delfino Plaza's two are in `tex/`. Empty texture = the Kind's default; `Leaf Material` empty = no leaves (the Toad). |
| Leaves | `Pollution Uv2 Materials` | which materials read the goop mask through the mesh's second UV set (the leaves). The body reads the first. Leave alone unless a re-export changes the UV layout. |
| Effects | `Effect Joint` | the joint the sparkle, the centre drips and the wash hang from: `koshi_null` on a Pianta, `jnt_body` on a Toad. |
| Effects | `Happy Effect`, `Happy Effect Scale` | the thank-you sparkle scene (`Fx/NpcSparklePianta.tscn` / `NpcSparkleToad.tscn`) and the game's 1.65 size. |
| Effects | `Dirty Drip Effect`, `Dirty Drip Frames` | the goop-ball bursts off a dirty idle, and the clip frames (60 fps) they fire on (a Pianta 28 52 76 128 152 176, a Toad 22 44). |
| Effects | `Foot Drip Effect`, `Foot Joint L` / `R`, `Foot Drip Frames L` / `R` | the Pianta's foot drips (left on 28 52 76, right on 128 152 176). Empty on a Toad. |
| Effects | `Wash Effect` | the balls flying off while the hose washes it (`NpcWashPianta` / `NpcWashToad`). |
| Effects | `Sink Effect` | the goop ripple ring and balls while it sinks (`Fx/GoopSinkRipple.tscn`; Mario's own copy sits on `Player.tscn`). |
| Pollution | `Goop From Layer` | on (default; a deviation at Jaysonn's word -- the game has one goop look per stage): a dirty NPC wears the goop of the **layer it sank in**. A Fire or Electric layer, or one with its own `Goop Texture`, puts that texture on its skin and colours its drips and wash from it (the brightest quarter of the texture = the light colour, the darkest quarter = the dark); a plain brown layer keeps the stage look (`tex/H_ma_rak`, the Delfino pair), so nothing seen so far changes. Name the two colours yourself with `Goop Light Color` / `Goop Dark Color` on the LAYER (alpha 0 = measured; on a brown layer they recolour the particles and keep the skin). An NPC placed `Pollution` > 0 on a layer takes that layer's goop at once. Off = the game: the one stage skin and colour row whatever it stood in. Mario's footprints, melt and spin fling follow the same rule through `Goop From Layer` on his `GoopMario` node (his own dirty tint stays the game's). |
| Textures | `Eye Texture` | the eye sheet (`tex/I_mom_eye01_IA8`, `I_mow_eye01_IA8`, `N_kinopio_me_open`): a dark oval with a white glint on a transparent sheet, from the bmd. Empty = the glb's own image, which the exporter got wrong (the Toad's was the toon ramp = white wedge eyes; the Piantas' had a bit-flipped grey pupil). The blinks and expressions come from `Tex Patterns` (below). |
| Textures | `Body Material Texture`, `Cloth Texture` | the Toad's two MASKS: `N_kinopio_cap` (black circle = the spot colour `Body Colors`, white = the cap `Body Colors 2`) and `N_kinopio_cloth` (black border = the trim `Cloth Colors`, white = the vest `Cloth Colors 2`). The glb had given both the pollution gradient -- a pink wash with no spots, an orange smear, a glossy look. Leave empty on a Pianta (its body samples no texture). |
| Textures | `Skin Texture`, `Mouth Texture` | the Toad's `N_kinopio_body` (face / hands / shorts) and `N_kinopio_kuti_close` (the default mouth; the talking / worried / wet shapes come from `Tex Patterns`). |
| Textures | `Tex Patterns` | the kind's texture pattern animations -- the disc's `.btp` files as `TexPatterns/<Kind>.tres`: the Piantas' `mom_wink` / `mow_wink` (two quick blinks every 1.5 s, on every clip that has no pattern of its own) plus one pattern per clip that does (`mom_wet`, `mom_happy`'s shut eyes, `mom_wait_yogore`'s dirty squint ...); the Toad's six (`kinopio_twait` blink + closed mouth; `ttalk` the mouth cycling while it talks; `twet` the open-mouth shout on happy / appear; `twet_a` the wet face when soaked; `twait2` / `tworry` the worried face of a dirty Toad). Each pattern lists the clips it plays with; the set's `Default` covers the rest. Every NPC starts its clip and its blink at a random frame (the game's `randomizeBckAndBtpFrame_`), so a crowd never blinks in unison. Empty = no blinking. |
| Burning | `Can Burn` | whether the `Burning` switch works on this kind (the game: only a normal male Pianta). Off on the female and the Toad. |
| Burning | `Fire Joint`, `Fire Joint On Luggage` | where the fire sits: `yashi_jnt` (the palm on his head), or the luggage part's `ef_null` when a worn part has that bone -- the game moves the fire onto the luggage. |
| Burning | `Burn With Luggage`, `Burn Luggage Part` | on (default): a `Burning` Pianta whose slots, extras and hand-placed parts carry no luggage gets `Burn Luggage Part` (empty = `Parts/Pianta/Luggage.tscn`) -- the game's data: all 8 burning placements in the shipped stages (Delfino Plaza episodes 6-10's runner, a Delfino secret stage, Pianta Village episode 3, a Pianta Village secret stage) set the luggage bit, so the fire is only ever seen on the bag. Off: the code's bare case, the fire on the palm. |
| Burning | `Water Hit Mark At` | where every fire-fighting hit's water-hit mark (`Fx/WaterHitMark.tscn`, the game's `ms_enm_wathit`) appears: **Fire** (default, a logged deviation at Jaysonn's word) = on the fire itself, the bag; **Drop** = where the drop hit the NPC's hit sphere; **Feet** = the game's own code (`behaveToHitObject_` emits it at the NPC's position -- the ground under it, every splat stacked there). Enemies (the Gooble) always mark the drop's point, as the game does. |
| Burning | `Burn Effect` | the fire scene (`Fx/NpcBurn.tscn`): the black smoke (`ms_npc_kokuen`) and the three fire emitters (`ms_moe_fire_a` tongues, `_b` glow, `_c` heat haze -- a refraction of the screen behind it). The three fires sit in the node group `sms_burn_strength` and shrink with the strength; the smoke does not. The scene keeps the world axes (the game binds it to a position, not the joint). |

Any `SmsNpcEffect` scene can stand in: a root `Node3D` with the `SmsNpcEffect` script (`Emit Frames` = how long it emits, 1 = one
burst, 0 = until the clip ends; `Drain Seconds` = the longest particle life) and `GPUParticles3D` children with `emitting` off.
The drips' and the wash's colours are the goop's: `env_color` / `prm_color` on their shader material (Delfino's brown pair; the
game's other maps' rows are in `EmitterViewObj.cpp` 644 if a level wants another colour). With `Goop From Layer` on, an NPC dirtied on
a Fire / Electric / own-texture layer gets a per-instance copy of that material carrying the layer's two colours (the scene file itself
is never edited); a brown layer leaves the scene's colours as they are.

## 7d. Signs -- `SignBoard.tscn` (pass 103: the game's board NPC and its board window)

In Sunshine a readable sign IS a **board NPC** (`NPCBoard`, `ACTOR_TYPE_NPC_BOARD`): its own model `boardNpc.bmd` -- the wooden board with
the Pianta-script glyphs on a post, 2.4 m tall -- drawn by the NPC manager, and the talk. (Pass 103 shipped it without the model and said
the sign was a separate mesh; pass 104 corrected that from `TBoardNpcManager::createModelData`.) `assets/Sms/Npc/SignBoard.tscn` is that
NPC: an `SmsNpc` on `Profiles/Board.tres` (`Kind = Board`) with the board model (`models/NPC/SignBoard/boardnpc.glb`, the disc's
`K_boardNpc_US` texture, the bmd's one-stage shading on the part shader), no collision (layer and mask 0; the capsule child is only the
game's 80 x 160 unit talk cylinder drawn as a gizmo, disabled) and no behaviour but the talk -- it never turns, walks, sinks, burns, reacts
to water or a stomp. Mario walks through it, as he does through the game's board NPC (its hit actor is `HIT_FILTER_NO_ATTACK` and its
perform skips the hit checks); a stage that wants a solid post gives it one in its own collision.

- Drop it where the sign stands, **facing out** (its -Z, the printed side, toward where the player stands; the back shows plain wood). The
  talk cone is the game's sit-talk cone (`mSLSitTalkAcceptDegree`, 100 degrees): Mario must be within 50 degrees of the sign's facing, within
  400 units, and facing it. The B cursor appears over it at the profile's `Body Height + Cursor Height` (the `npcBoard.prm` values on
  `Board.tres`). `Body Material Texture` on the profile swaps the board's texture (empty = the disc's).
- `Lines` are the pages. A board page holds up to **six** lines (Enter = a new line); a line wider than the box (257 units in the system font
  at 20 x 26) breaks by words onto the next line, and lines past six flow onto the next page. Tags (`<c3>`, `<w=8>`, `<yesno>`) are dropped
  on a board, as the game's `setupBoardTextBox` skips them: no colours, no choice window on a sign.
- What shows: the board window (`message_board_1.blo`) -- the translucent blue plank with its seven ruled lines, the text centred line by
  line, the camera on TALK_A -- slides in from below (25 ticks) once the camera has settled (nothing is drawn before that, as in the game),
  the mark group fades in and the arrow pulses (more pages) or the dot (last page). A or B flips the page (the text fades out and back in);
  on the last page A or B closes it: the camera comes straight back while the plank slides up off the screen. Game quirk kept: after a flip to
  the last page the arrow stays at whatever alpha the flip caught it at while the dot pulses (nothing in the game hides it).
- Knobs: `Board Page Fade Step` on the balloon (`TalkBalloon.tscn`) -- the page flip's text fade per tick. **8 by default (your ask, pass
  104: 0.27 s each way); the game's is 4 (0.53 s).** The arrow / dot pulse and the mark group's fade-in keep the game's 4. Also the
  balloon's `Size` (the frame's scale), `Auto Wrap`. `Park Offset` moves only the balloon, never the board.
- Other NPC kinds are untouched: give a Pianta or a Toad the Board profile and it loses its model handling -- make a sign, not a profile swap.

## 8. If it breaks

- `[SMS npc] X: Profile is null` -> the instance has a `Profile = null` override (an editor save made while a class was not `[Tool]`);
  select the NPC and revert `Profile` / `Outfit` in the inspector. (Pass 91/92.)
- `InvalidCastException ... 'Godot.Resource'` in the editor -> a custom Resource class an NPC exports lost its `[Tool]` attribute; all
  five (`SmsNpcProfile`, `SmsNpcOutfitCatalog`, `SmsNpcOutfitSlot`, `SmsNpcPart`, `SmsNpcPartMaterial`) must keep it. After adding a
  new `[Tool]` class, build and **close and reopen the project** once.
- A piece floats at the NPC's feet / centre -> its `Joint` is not on that skeleton (see section 2, across species).
- `... is not an SmsNpcPart scene` warning -> the scene's root has no `SmsNpcPart` script.
- A piece is grey -> no `SmsNpcPartMaterial` matched its surface's material name (check `Material Name` against the mesh's material).
