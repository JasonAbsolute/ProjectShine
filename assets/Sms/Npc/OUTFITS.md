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
| Talk | `Lines`, `CanTalk` | the balloon's pages (Enter = a new line, up to three a page); empty or off = no cursor |
| Take | `Can Be Taken` | B lifts it instead of talking (the game's stage-script flag); carried, put down or pitched |
| Throw | `Throws Mario`, `Throw Speed`, `Throw Angle Deg` | the Pianta Village launcher: when the talk ends it hurls Mario up and over its own head, to its BACK, at the speed (units a tick) and angle. It takes the talk only within 200 units (not 400). Stand it with its back to the destination. |
| Pollution | `Pollution` | 0..1: placed already goop-covered (dirty wait, no talk, no turning, the head straight) until the hose washes it clean (125 hits at the Pianta's 0.008) |
| Pollution | `Thank You Coin` | a scene to pop out 40 frames after the talk that follows a wash (the game's TNpcCoin). Empty = none. |
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

Any `SmsNpcEffect` scene can stand in: a root `Node3D` with the `SmsNpcEffect` script (`Emit Frames` = how long it emits, 1 = one
burst, 0 = until the clip ends; `Drain Seconds` = the longest particle life) and `GPUParticles3D` children with `emitting` off.
The drips' and the wash's colours are the goop's: `env_color` / `prm_color` on their shader material (Delfino's brown pair; the
game's other maps' rows are in `EmitterViewObj.cpp` 644 if a level wants another colour).

## 8. If it breaks

- `[SMS npc] X: Profile is null` -> the instance has a `Profile = null` override (an editor save made while a class was not `[Tool]`);
  select the NPC and revert `Profile` / `Outfit` in the inspector. (Pass 91/92.)
- `InvalidCastException ... 'Godot.Resource'` in the editor -> a custom Resource class an NPC exports lost its `[Tool]` attribute; all
  five (`SmsNpcProfile`, `SmsNpcOutfitCatalog`, `SmsNpcOutfitSlot`, `SmsNpcPart`, `SmsNpcPartMaterial`) must keep it. After adding a
  new `[Tool]` class, build and **close and reopen the project** once.
- A piece floats at the NPC's feet / centre -> its `Joint` is not on that skeleton (see section 2, across species).
- `... is not an SmsNpcPart scene` warning -> the scene's root has no `SmsNpcPart` script.
- A piece is grey -> no `SmsNpcPartMaterial` matched its surface's material name (check `Material Name` against the mesh's material).
