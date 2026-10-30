using Godot;
using System.Collections.Generic;

/// <summary>
/// SMS pass 129 -- TSamboFlower (hanasambo.cpp): the sunflower bud a buried Pokey (<see cref="SmsHanaSambo"/>) or Pokey Head
/// (<see cref="SmsSamboHead"/>) marks its spot with, and the hose opens.
///
/// THE GAME: flower.bmd with the stage's flower_orange.bmt (the orange petals), three clips -- flower_wait (the closed bud, looping),
/// flower_hit (the bud opening, 30 frames) and flower_fwait (the open bloom, looping). receiveMessage(SPRAYED_BY_WATER) on a closed one ->
/// bloom(): PARTICLE_MS_SMB_KAFUN_OW (the pollen puff), flower_hit, MSD_SE_OBJ_FLOWER_OPEN_N; moveObject: when flower_hit ends -> flower_fwait.
/// The stage's own coin-unit flowers (unk160) close again after BloomTimer (300) and a whole unit open at once pays a coin ring -- a Pokey's
/// flower (makeOneEnemyAppear: unk160 false) stays open. hide(): no collision, fwait, LIVE_FLAG_DEAD (not drawn). The owner's initFlower:
/// collision on, UNK10 (no ground snap), alive, placed at the owner's ground point. isBloomEnd = the fwait clip is on.
/// Its hit actor is a 30 x 120 unit cylinder (samboflower.prm) that only the WATER tests (TObjHitCheck::checkWater: each flying drop's 50 x 80
/// cylinder against it) -- Mario and every actor walk through it. So it is no physics body here either: a shown bud registers itself and
/// SmsWaterDrops asks <see cref="ZoneAt"/> for every flying drop, as it asks the hidden-coin spots (a body would be a floor to the enemies' ground
/// rays and the hose's ray alike).
/// DEVIATIONS: the coin units (a stage system, no stage here); the leaves are the owner's (<see cref="SmsSamboLeaf"/>).
/// </summary>
[GlobalClass]
public partial class SmsSamboFlower : Node3D, ISmsSprayTarget
{
    [ExportGroup("Effects (empty = none)")]
    /// <summary>PARTICLE_MS_SMB_KAFUN_OW at the bloom (the pollen puff off the opening bud). Any one-shot scene; empty = none.</summary>
    [Export] public PackedScene BloomFxScene;
    /// <summary>MSD_SE_OBJ_FLOWER_OPEN_0 at the bloom. Unset = silent.</summary>
    [Export] public AudioStreamPlayer3D BloomSound;
    [ExportGroup("Placement")]
    /// <summary>On: a flower placed by hand in a level stands as a closed bud from the start. Off: it starts hidden and waits for an owner's Show
    /// (what a Pokey's flower does).</summary>
    [Export] public bool StartsShown = true;

    /// <summary>samboflower.prm: mSLBodyRadius 30, mSLHeadHeight 120 -- the hit actor the water tests.</summary>
    public const float BodyRadius = 30f, HeadHeight = 120f;
    /// <summary>TObjHitCheck::checkWater's drop: a 50 x 80 cylinder.</summary>
    private const float DropRadius = 50f, DropHeight = 80f;

    private static readonly List<SmsSamboFlower> ShownBuds = new();
    /// <summary>The shown bud whose hit cylinder a drop at <paramref name="dropMetres"/> overlaps, or null.</summary>
    public static SmsSamboFlower ZoneAt(Vector3 dropMetres)
    {
        if (ShownBuds.Count == 0) return null;
        float u = SmsUnits.UnitsPerMetre;
        Vector3 d = dropMetres * u;
        for (int i = ShownBuds.Count - 1; i >= 0; i--)
        {
            var f = ShownBuds[i];
            if (f == null || !IsInstanceValid(f)) { ShownBuds.RemoveAt(i); continue; }
            Vector3 s = f.GlobalPosition * u;
            if (d.Y > s.Y + HeadHeight || d.Y + DropHeight < s.Y) continue;
            float r = DropRadius + BodyRadius, dx = d.X - s.X, dz = d.Z - s.Z;
            if (r * r > dx * dx + dz * dz) return f;
        }
        return null;
    }

    private Node3D _model; private AnimationPlayer _anim;
    private bool _bloomed;    // unk150: opening or open
    private bool _shown;

    /// <summary>unk150: the hose has opened it (opening or open).</summary>
    public bool Bloomed => _bloomed;
    /// <summary>isBloomEnd: the open bloom is on.</summary>
    public bool Open => _anim != null && _anim.CurrentAnimation == "flower_fwait";
    public bool Shown => _shown;
    public string ClipName => _anim?.CurrentAnimation ?? "";

    public override void _Ready()
    {
        _model = GetNodeOrNull<Node3D>("Model");
        _anim = GetNodeOrNull<AnimationPlayer>("Model/AnimationPlayer");
        if (_anim != null)
        {
            foreach (string n in new[] { "flower_wait", "flower_fwait" })
                if (_anim.HasAnimation(n)) _anim.GetAnimation(n).LoopMode = Animation.LoopModeEnum.Linear;
            _anim.AnimationFinished += name => { if (name == "flower_hit" && _bloomed) Play("flower_fwait"); };   // moveObject: the opening done -> the open loop
        }
        if (StartsShown) Show(GlobalPosition); else Hide();
    }
    public override void _ExitTree() { ShownBuds.Remove(this); }

    /// <summary>initFlower: shown at the owner's ground point as a closed bud (or, re-shown after a bloom, open: the clip is kept).</summary>
    public void Show(Vector3 groundPoint)
    {
        GlobalPosition = groundPoint;
        _shown = true;
        if (_model != null) _model.Visible = true;
        if (!ShownBuds.Contains(this)) ShownBuds.Add(this);
        if (_anim != null && !_bloomed) Play("flower_wait");
        else if (_anim != null && _bloomed && _anim.CurrentAnimation != "flower_hit") Play("flower_fwait");
    }
    /// <summary>hide(): no collision, the open pose, not drawn.</summary>
    public void Hide()
    {
        _shown = false;
        if (_model != null) _model.Visible = false;
        ShownBuds.Remove(this);
        if (_anim != null) Play("flower_fwait");
    }
    /// <summary>TSpineEnemy::reset for a re-launch: the closed bud again.</summary>
    public void ResetBud() { _bloomed = false; if (_anim != null) Play("flower_wait"); }

    /// <summary>bloom(): the pollen puff, flower_hit (then fwait when it ends), the sound.</summary>
    public void Bloom()
    {
        _bloomed = true;
        if (BloomFxScene != null)
        {
            var fx = BloomFxScene.Instantiate<Node3D>();
            fx.TopLevel = true; GetParent().AddChild(fx); fx.GlobalPosition = GlobalPosition;
            if (fx is SmsFxBurst b) b.Play(fx.GlobalTransform);
            else if (fx is GpuParticles3D gp) { gp.OneShot = true; gp.Emitting = true; gp.Finished += gp.QueueFree; }
        }
        if (BloomSound != null && BloomSound.Stream != null) BloomSound.Play();
        Play("flower_hit");
    }
    /// <summary>receiveMessage(SPRAYED_BY_WATER): a closed, shown bud opens; the drop is taken either way (returns 1).</summary>
    public bool SmsSprayedByWater(Vector3 hitPos)
    {
        if (!_shown) return false;
        if (!_bloomed) Bloom();
        return true;
    }

    private void Play(string clip)
    {
        if (_anim == null || !_anim.HasAnimation(clip)) return;
        if (_anim.CurrentAnimation == clip && _anim.IsPlaying()) return;
        _anim.Play(clip); _anim.Seek(0f, true);
    }
}
