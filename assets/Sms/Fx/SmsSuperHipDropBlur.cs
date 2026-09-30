using Godot;

/// <summary>
/// The super hip drop's fall effect: MarioJump.cpp 1142 -- once the hip drop has fallen mSuperHipAttackCt ticks (jump.prm: 80) the state is
/// 3 and every tick of the fall calls emitBlurHipDropSuper() (MarioParticle.cpp 552) instead of emitBlurHipDrop(): ms_m_sphipd_a / b / c
/// / d bound to the 'center' joint. The scene assets/Sms/Fx/SuperHipDropBlur.tscn (gen_fx_super.py from the .jpa fields): a -- red
/// flames (P_msm_glow3) riding with him, 2 a frame from a 5-unit ring below his centre, out at 19.5 units a frame and lifted 2.5 a frame
/// squared, 150 x 75 units for 12 frames, pink -> red; b -- glow sparkles (P_msm_glow1) left in the world around him, one every two
/// frames in a 25..40-unit shell, twinkling for a second; c -- the fire streak (P_msm_blurmsk1 scrolled 0.3 a frame under the fire colour,
/// 150 units wide, 20 frames long, additive: the SmsHipDropBlur ribbon with the additive stripe shader); d -- a dark orange glow
/// (P_msm_glow4, 112 units) left every four frames 20 units above him. Active while the super fall runs, like the normal blur.
/// </summary>
public partial class SmsSuperHipDropBlur : SmsFollowEffect
{
    /// <summary>ms_m_sphipd_c: the fire streak (an SmsHipDropBlur with the additive shader, WidthUnits 150, LifeFrames 20).</summary>
    [Export] public SmsHipDropBlur Stripe;

    /// <summary>ms_m_sphipd_a: the flames riding with him.</summary>
    [Export] public GpuParticles3D Flames;

    /// <summary>ms_m_sphipd_b: the sparkles left behind.</summary>
    [Export] public GpuParticles3D Sparkles;

    /// <summary>ms_m_sphipd_d: the glows left behind.</summary>
    [Export] public GpuParticles3D Glow;

    private bool _active;

    /// <summary>On while the super fall runs (the game emits every tick of it); off stops the emitters and lets what is out finish.</summary>
    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value)
                return;
            _active = value;
            if (Stripe != null)
                Stripe.Active = value;
            foreach (var p in new[] { Flames, Sparkles, Glow })
                if (p != null)
                    p.Emitting = value;
        }
    }

    public override void _Ready()
    {
        if (Stripe == null || Flames == null || Sparkles == null || Glow == null)
            GD.PushWarning($"[SMS fx] {GetPath()}: the scene's Stripe / Flames / Sparkles / Glow exports are not all set (see assets/Sms/Fx/SuperHipDropBlur.tscn)");
        foreach (var p in new[] { Flames, Sparkles, Glow })
            if (p != null)
            {
                p.OneShot = false;
                p.Emitting = false;
            }
    }

    /// <summary>Bind the whole effect to Mario: this node and the stripe both follow his 'center' joint.</summary>
    public override void Bind(Node3D owner)
    {
        base.Bind(owner);
        Stripe?.Bind(owner);
    }
}
