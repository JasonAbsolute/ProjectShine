using Godot;

/// <summary>
/// gpCameraShake (TCameraShake, Camera/camerashake.cpp): the game's camera shake. startShake(mode, scale) takes a free slot of four (or
/// the one soonest done) and loads a mode's .prm (SmsCameraShakeMode): for Time ticks each slot adds Amp x sin(Vel x tick) to the
/// camera's vertical and horizontal polar angles about its target and to the roll of its up vector, the amplitudes falling linearly
/// to zero (mDec = Amp / Time); the camera's own logic never sees it -- execShake runs last, in CPolarSubCamera::calcFinalPosAndAt_,
/// on the final eye position and up (cameragc.cpp 832), 120 Hz like the rest of the camera.
/// The port keeps that separation with a RENDER camera: while any shake is active, <see cref="Camera"/> copies the current camera
/// (whatever made itself current -- SunshineCamera, the Y camera, a demo or reward camera) each frame, orbits the eye about the view
/// axis at Mario's distance by the shake's angles, rolls the up vector, and is the current camera; when the last shake ends it hands
/// current back to the camera it copied. No script's camera transform is ever touched, so smoothing and look-ahead are unaffected.
/// Not resolved in the decomp: execShake's roll axis reads as the eye's displacement direction (a zero vector for a pure roll) -- the
/// port rolls about the view direction, which is what a roll of the up vector is for.
/// The scene is assets/Sms/CameraShake.tscn, instanced under Mario in Player.tscn (Mario.cs finds it: SmsCameraShake.Current). Mario:
/// the hip drop's landing (MarioJump.cpp 1153, mode 0 / the super hip drop's 1156, mode 0x27 = shakeKoopaHipdrop), damage
/// (MarioCollision.cpp 380, mode 2, not for the upper-body type 3). Enemies and objects call Start too (tamaNoko, the bosses, ...).
/// The rumbles beside them are not ported.
/// </summary>
public partial class SmsCameraShake : Node
{
    public enum Kind { HipDrop, Damage, SuperHipDrop }

    /// <summary>The render camera (a child Camera3D): current only while a shake runs.</summary>
    [Export] public Camera3D Camera;
    /// <summary>shakeHipDrop.prm -- the ground pound's landing.</summary>
    [Export] public SmsCameraShakeMode HipDrop;
    /// <summary>shakeDamage.prm -- a hit (damageExec with a knockdown type).</summary>
    [Export] public SmsCameraShakeMode Damage;
    /// <summary>shakeKoopaHipdrop.prm -- the super hip drop's landing (startShake(0x27)).</summary>
    [Export] public SmsCameraShakeMode SuperHipDrop;
    /// <summary>Jaysonn's knob: every shake's angles x this (1 = the game).</summary>
    [Export(PropertyHint.Range, "0,3,0.05")] public float Strength = 1f;
    /// <summary>Off: Start does nothing (the camera is never taken over).</summary>
    [Export] public bool Enabled = true;

    public static SmsCameraShake Current { get; private set; }

    private const int Slots = 4;                       // ARRAY_COUNT(mShakeInfo)
    private const float TicksPerSecond = 120f;
    private const float BamToRad = Mathf.Pi / 32768f;

    private struct Angle { public float Amp, Dec; public int Vel; }
    private struct Slot
    {
        public bool Keep, Decreasing;
        public int Duration, Frame;                    // u16 mDuration, mFrame; Duration != 0 = active
        public Angle X, Y, Z;
        public bool Active => Duration != 0;
    }

    private readonly Slot[] _slots = new Slot[Slots];
    private float _acc;
    private float _v, _h, _roll;                       // this tick's summed angles, BAM
    private Camera3D _source;                          // the camera the render camera copies
    private Node3D _mario;

    public override void _EnterTree() => Current = this;
    public override void _ExitTree() { if (Current == this) Current = null; }

    public override void _Ready()
    {
        ProcessPriority = 1000;                        // after every camera script's frame
        if (Camera == null)
            GD.PushWarning($"[SMS camera] {GetPath()}: the scene's Camera export is not set (see assets/Sms/CameraShake.tscn) -- no shake");
        else
            Camera.Current = false;
        _mario = GetParent() as Node3D;
    }

    /// <summary>startShake(mode, scale): a free slot, or the decreasing one with the least left.</summary>
    public void Start(Kind kind, float scale = 1f)
    {
        if (!Enabled || Camera == null)
            return;
        var mode = kind switch { Kind.HipDrop => HipDrop, Kind.Damage => Damage, _ => SuperHipDrop };
        if (mode == null || mode.TimeTicks <= 0)
            return;                                    // a mode with Time 0 (the game's wall jump) shakes nothing
        int i = FreeSlot();
        ref Slot s = ref _slots[i];
        s.Keep = false; s.Decreasing = false; s.Duration = mode.TimeTicks; s.Frame = 0;
        float k = scale * Strength;
        s.X = Set(mode.AmpX, mode.VelX, mode.TimeTicks, k);
        s.Y = Set(mode.AmpY, mode.VelY, mode.TimeTicks, k);
        s.Z = Set(mode.AmpZ, mode.VelZ, mode.TimeTicks, k);
    }

    /// <summary>setShakeAngleOne_: amp x scale (a negative scale flips the sine too), the per-tick decrement amp / duration.</summary>
    private static Angle Set(float amp, int vel, int duration, float scale)
    {
        amp *= scale;
        if (scale < 0f) { amp = -amp; vel = -vel; }
        return new Angle { Amp = amp, Dec = amp / duration, Vel = vel };
    }

    /// <summary>getUseShakeData_: the first inactive slot, else the decreasing slot with the fewest ticks left.</summary>
    private int FreeSlot()
    {
        for (int i = 0; i < Slots; i++)
            if (!_slots[i].Active)
                return i;
        int best = 0, min = int.MaxValue;
        for (int i = 0; i < Slots; i++)
            if (_slots[i].Decreasing)
            {
                int left = _slots[i].Duration - _slots[i].Frame;
                if (left < min) { min = left; best = i; }
            }
        return best;
    }

    private bool AnyActive()
    {
        for (int i = 0; i < Slots; i++)
            if (_slots[i].Active)
                return true;
        return false;
    }

    /// <summary>execShake's per-tick sum: the angles this tick, the frames and decays stepped.</summary>
    private void Tick()
    {
        _v = _h = _roll = 0f;
        for (int i = 0; i < Slots; i++)
        {
            ref Slot s = ref _slots[i];
            if (!s.Active)
                continue;
            _v += (int)(s.X.Amp * Mathf.Sin((short)(s.X.Vel * s.Frame) * BamToRad));
            _h += (int)(s.Y.Amp * Mathf.Sin((short)(s.Y.Vel * s.Frame) * BamToRad));
            _roll += (int)(s.Z.Amp * Mathf.Sin((short)(s.Z.Vel * s.Frame) * BamToRad));
            s.Frame++;
            if (s.Keep)
            {
                s.Duration++;
                s.Keep = false;
            }
            else
            {
                s.Decreasing = true;
                s.X.Amp -= s.X.Dec; s.Y.Amp -= s.Y.Dec; s.Z.Amp -= s.Z.Dec;
                if (s.Frame >= s.Duration)
                    s = default;                           // reset()
            }
        }
    }

    public override void _Process(double delta)
    {
        if (Camera == null)
            return;
        bool active = AnyActive();
        if (active)
        {
            _acc += (float)delta * TicksPerSecond;
            int n = 0;
            while (_acc >= 1f && n++ < 16) { _acc -= 1f; Tick(); }
            if (_acc >= 1f) _acc = 0f;
        }
        var cur = GetViewport().GetCamera3D();
        if (cur != null && cur != Camera)
            _source = cur;                                 // whoever made itself current is the camera we render for
        if (!active || _source == null || !IsInstanceValid(_source))
        {
            if (Camera.Current)
            {
                Camera.Current = false;
                if (_source != null && IsInstanceValid(_source))
                    _source.MakeCurrent();                 // hand the picture back
            }
            _v = _h = _roll = 0f;
            return;
        }
        Copy(_source, Camera);
        Apply(_source.GlobalTransform);
        if (!Camera.Current)
            Camera.MakeCurrent();
    }

    /// <summary>The eye orbited about the view axis at Mario's distance by the vertical / horizontal angles (CLBCrossToPolar ->
    /// CLBPolarToCross about the camera's target), the up rolled about the view direction, as the render camera's transform.</summary>
    private void Apply(Transform3D src)
    {
        Vector3 eye = src.Origin;
        Vector3 fwd = -src.Basis.Z;
        float dist = 5f;
        if (_mario != null)
            dist = Mathf.Max(0.5f, (_mario.GlobalPosition + Vector3.Up * 0.8f - eye).Dot(fwd));
        Vector3 pivot = eye + fwd * dist;                  // the camera's target (unk124), taken on its own view axis
        Vector3 d = eye - pivot;
        float r = d.Length();
        if (r < 1e-4f) { Camera.GlobalTransform = src; return; }
        float el = Mathf.Asin(Mathf.Clamp(d.Y / r, -1f, 1f)) + _v * BamToRad;
        float az = Mathf.Atan2(d.X, d.Z) + _h * BamToRad;
        el = Mathf.Clamp(el, -1.55f, 1.55f);
        Vector3 nd = new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az)) * r;
        Vector3 newEye = pivot + nd;
        Vector3 view = (pivot - newEye).Normalized();
        Vector3 up = src.Basis.Y.Rotated(view, -_roll * BamToRad); // TRotation3(dir, -roll): the up vector rolled about the view
        if (Mathf.Abs(up.Dot(view)) > 0.999f)
            up = Vector3.Up;
        Camera.GlobalTransform = new Transform3D(Basis.LookingAt(view, up), newEye);
    }

    private static void Copy(Camera3D from, Camera3D to)
    {
        to.Projection = from.Projection;
        to.Fov = from.Fov; to.Size = from.Size; to.Near = from.Near; to.Far = from.Far;
        to.KeepAspect = from.KeepAspect; to.CullMask = from.CullMask;
        to.Environment = from.Environment; to.Attributes = from.Attributes;
        to.HOffset = from.HOffset; to.VOffset = from.VOffset;
        to.DopplerTracking = from.DopplerTracking;
    }
}
