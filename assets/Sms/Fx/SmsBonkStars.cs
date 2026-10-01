using Godot;

/// <summary>
/// The game's ms_dmg_c stars: emitParticle(PARTICLE_MS_DMG_C) at mPosition (his feet; unbound, world space, the emitter's axis UP) when a
/// jump or a dive hits a wall above ClashSpeed and knocks him into JUMP_SHORT_BACK_DOWN (MarioJump.cpp 164, 464, MarioPhysics.cpp 53 --
/// the bonk), on the first tick of every ground knockdown status (backDown / foreDown / shortBackDown / shortForeDown / safeBackDown /
/// safeForeDown / catchDown, MarioRun.cpp 1399-1470 -- the landing after a bonk or an air knockdown, and a knockdown taken on the
/// ground), and as one of the three damage effects (dmg_a / b / c at calcDamagePos). The scene assets/Sms/Fx/BonkStars.tscn
/// (gen_fx_super.py from the .jpa fields): spinning P_msm_hoshi stars (65 units; yellow / pink / peach, one colour per star) that fly
/// in straight lines under a 0.96-a-frame air resistance for 75 frames (+-46 %), flipping from a tall sliver to a flat one, faded in by
/// 12.5 % and out from 80 %.
///
/// The launch is the emitter's (BEM1, noclip JPA.ts calcVolumeSphere / init_p): each star spawns on the upper half of a 10-unit shell 5
/// units up (volume Sphere, sweep 0.5) and leaves with velAxis 10 units a frame straight out from the up axis (the spawn point's
/// horizontal direction) plus velDir 18 along the axis tilted by spread 0.4 x [-1, 1] x 180 = up to 72 degrees at a random azimuth,
/// the sum x (1 +- 0.3). Godot's ParticleProcessMaterial has no per-particle direction with a shared speed curve, so the launch is
/// sampled: ApplyLaunch draws <see cref="Directions"/> stars by the game's rule, puts each one's DIRECTION on the shell as an emission
/// point and lets the material's radial velocity (from the shell's centre, the decay curve of the scene) carry it along that line; the
/// speeds' 5th..95th percentile are the radial min / max (the direction-speed pairing is the one thing not kept).
/// </summary>
public partial class SmsBonkStars : Node3D
{
    /// <summary>ms_dmg_c: the stars.</summary>
    [Export] public GpuParticles3D Stars;

    /// <summary>The bonk's sound, his to fill.</summary>
    [Export] public AudioStreamPlayer3D Sound;

    /// <summary>How many stars a burst throws. DEVIATION (Jaysonn, 2026-09-30: "add a few more stars"): 10; the game's ms_dmg_c emits 6
    /// (rate 6, one frame).</summary>
    [Export(PropertyHint.Range, "1,64,1")] public int Count = 10;

    /// <summary>A bonk's stars leave along the impact (the emitter's axis turned from up to the way he is knocked back, away from the wall).
    /// OFF: the game emits dmg_c with the axis up for a bonk too, and Jaysonn confirmed that look (2026-09-30: "i was wrong they do go
    /// up"). Kept as a knob. A hit's and a landing's stars always use the game's up.</summary>
    [Export] public bool AimAlongImpact = false;

    /// <summary>velDir: the speed along the emitter's axis (up), game units a frame (60 Hz). The game: 18.</summary>
    [Export(PropertyHint.Range, "0,60,0.5")] public float UpSpeed = 18f;

    /// <summary>velAxis: the speed straight out from the axis, game units a frame. The game: 10.</summary>
    [Export(PropertyHint.Range, "0,60,0.5")] public float OutSpeed = 10f;

    /// <summary>How far the along-axis part may tilt from up, degrees: the game's spread 0.4 x 180 = 72 (uniform in angle), which puts
    /// the stars between about 12 and 90 degrees of elevation, the middle one at 48. Lower it for a tighter, taller spray.</summary>
    [Export(PropertyHint.Range, "0,180,1")] public float SpreadDeg = 72f;

    /// <summary>velRatio: each star's speed x (1 +- this). The game: 0.3.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float SpeedRandom = 0.3f;

    private const int Directions = 256;              // sampled launch directions baked into the emission points
    private const float ShellUnits = 10f;            // volume Sphere size 10
    private const float PivotUnits = 5f;             // emitter trs (0, 5, 0)
    private const ulong Seed = 0x6D735F646D675F63;   // "ms_dmg_c": the same sample set every boot

    public override void _Ready()
    {
        if (Stars == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Stars export is not set (see assets/Sms/Fx/BonkStars.tscn)");
        if (Stars != null)
        {
            Stars.OneShot = true;
            Stars.Emitting = false;
            Stars.Amount = Mathf.Max(1, Count);
        }
        ApplyLaunch();
    }

    /// <summary>The launch sampled by the game's rule into the material: the emission points (one per direction, on the shell around the
    /// pivot) and the radial speed range, from UpSpeed / OutSpeed / SpreadDeg / SpeedRandom.</summary>
    private void ApplyLaunch()
    {
        if (Stars?.ProcessMaterial is not ParticleProcessMaterial mat)
            return;
        var rng = new RandomNumberGenerator { Seed = Seed };
        float spread = Mathf.DegToRad(SpreadDeg);
        Vector3 pivot = Vector3.Up * SmsUnits.UnitsToMetres(PivotUnits);
        float shell = SmsUnits.UnitsToMetres(ShellUnits);
        var img = Image.CreateEmpty(Directions, 1, false, Image.Format.Rgbf);
        var speeds = new float[Directions];
        for (int i = 0; i < Directions; i++)
        {
            // calcVolumeSphere, sweep 0.5: angle in [-90, 90] deg, x in [0, 180] -> the upper half shell; velAxis = the point's horizontal direction
            float angle = (rng.Randf() - 0.5f) * Mathf.Pi;
            float x = rng.Randf() * Mathf.Pi;
            var p = new Vector3(Mathf.Cos(x) * Mathf.Sin(angle), Mathf.Sin(x), Mathf.Cos(x) * Mathf.Cos(angle));
            var axis = new Vector3(p.X, 0f, p.Z);
            axis = axis.LengthSquared() > 1e-8f ? axis.Normalized() : Vector3.Right;
            // velDir: computeModelMatrixR(0, spread x randY x pi, randZ x pi) applied to the axis -- a tilt uniform in [-spread, spread] at a random azimuth
            float tilt = spread * (rng.Randf() * 2f - 1f);
            float az = rng.Randf() * Mathf.Pi;
            var dir = new Vector3(Mathf.Sin(tilt) * Mathf.Cos(az), Mathf.Cos(tilt), Mathf.Sin(tilt) * Mathf.Sin(az));
            Vector3 v = axis * OutSpeed + dir * UpSpeed;
            float len = v.Length();
            Vector3 d = len > 1e-6f ? v / len : Vector3.Up;
            speeds[i] = len * (1f + (rng.Randf() * 2f - 1f) * SpeedRandom);
            Vector3 pt = pivot + d * shell; // the radial velocity from the pivot carries the star along d
            img.SetPixel(i, 0, new Color(pt.X, pt.Y, pt.Z));
        }
        System.Array.Sort(speeds);
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Points;
        mat.EmissionPointTexture = ImageTexture.CreateFromImage(img);
        mat.EmissionPointCount = Directions;
        mat.VelocityPivot = pivot;
        mat.RadialVelocityMin = SmsUnits.FxSpeedToMps(speeds[(int)(0.05f * (Directions - 1))]);
        mat.RadialVelocityMax = SmsUnits.FxSpeedToMps(speeds[(int)(0.95f * (Directions - 1))]);
    }

    /// <summary>The burst at <paramref name="at"/> (the game's mPosition / calcDamagePos). <paramref name="impact"/> is the way he is
    /// thrown by a bonk (the wall's normal) -- with AimAlongImpact the emitter's axis follows it; Vector3.Up (or zero) keeps the game's
    /// upward spray.</summary>
    public void Play(Vector3 at, Vector3 impact)
    {
        Vector3 axis = AimAlongImpact && impact.LengthSquared() > 1e-6f ? impact.Normalized() : Vector3.Up;
        Vector3 x = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.999f ? Vector3.Right : Vector3.Up.Cross(axis).Normalized();
        Vector3 z = x.Cross(axis);
        GlobalTransform = new Transform3D(new Basis(x, axis, z), at);
        Stars?.Restart();
        if (Sound != null && Sound.Stream != null)
            Sound.Play();
    }
}
