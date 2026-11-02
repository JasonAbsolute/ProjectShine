using System;
using Godot;

/// <summary>
/// KING BOO'S SLOT MACHINE -- TTelesaSlot : TSlotDrum (Enemy/bosstelesa.cpp 520-790 with MoveBG/MapObjSirena.cpp's TSlotDrum; ACTOR_TYPE_SROT_RULET,
/// the srotRulet model's three drums + the srot_waku frame) for SMS pass 153. Three 8-face reels (unk168 = 45 deg a face) read by angle:
/// 0-44 COIN, 45-89 ENEMY, 90-134 JACKPOT(3), 135-179 FRUIT, then again -- getResultFromAng. moveStart spins them (unk158 2.0 deg a tick: the left -1,
/// the middle -0.8, the right +1) HELD (unk198) until released; a released drum decelerates 0.01 a tick to 0.5 and then creeps to the next 45-deg stop
/// (SLT_STOP). King Boo's roll clip ends -> forceStopSlot(1): with FirstHitCollectRate (0.6; 0.9 at Mario's last hit point) the TARGET is FRUIT (COIN
/// if his HP is 3 or under) and the middle drum is held until its forecast lands on it; else the target is wherever the middle will fall. The target is
/// re-read: the same as last round's result -> JACKPOT; COIN with no coin rounds left -> ENEMY, or with Mario at 6+ HP -> JACKPOT. When a drum stops,
/// every drum still held is, with HitCollectRate (1.0 shipped: always), held until ITS forecast is the target -- so the shipped slot always lands
/// three of a kind. All stopped -> the boss's fanfale. The port: the reels' angles turn the drum joints (bones 1..3) about X each tick; the forecast
/// simulates the decel to the stop as the game does.
/// </summary>
public partial class SmsTelesaSlot : Node3D
{
    public const int Coin = 0, Enemy = 1, Fruit = 2, Jackpot = 3, Mixed = -1;
    public const float StartSpeed = 2.0f, Decel = 0.01f, CreepSpeed = 0.5f;   // unk158 / unk15C / unk160
    public const int FaceDeg = 45;                                           // unk168
    [Export] public AudioStreamPlayer3D SpinSound, StopSound;

    private readonly float[] _ang = new float[3], _spd = new float[3];       // unk13C / unk138
    private readonly bool[] _held = new bool[3], _toTarget = new bool[3];     // unk198 / unk1A8
    private bool _rolling, _firstStopPending = true;                           // unk19B / unk19C
    private int _target = 2;                                                   // unk1A4
    private Skeleton3D _skel; private readonly int[] _drumBone = { -1, -1, -1 };
    private float _accum;
    private int _spinTickToggle;
    /// <summary>The boss this slot belongs to (unk1A0) -- it reads his rates and gets the fanfare.</summary>
    public SmsBossTelesa Boss;
    /// <summary>For the drives.</summary>
    public bool IsRolling => _held[0] || _held[1] || _held[2] || _spd[0] != 0f || _spd[1] != 0f || _spd[2] != 0f;
    public bool AnyHeld => _held[0] || _held[1] || _held[2];
    public int Target => _target;
    public float AngleOf(int i) => _ang[i];
    public float SpeedOf(int i) => _spd[i];
    public bool HeldOf(int i) => _held[i];
    public int ForcedResult = int.MinValue;   // a drive's forced target (int.MinValue = none)

    public override void _Ready()
    {
        var model = GetNodeOrNull<Node3D>("Drums");
        if (model != null)
        {
            foreach (Node n in model.FindChildren("*", "Skeleton3D", true, false)) { _skel = n as Skeleton3D; break; }
            if (_skel != null)
            {
                // the J3D joints 1..3 are the drums (partsRollCallback: joint i rolls by unk13C[i - 1]); the glb names them "02" / "01" / "00" in joint order
                for (int i = 0; i < 3; i++) if (_skel.GetBoneCount() > i + 1) _drumBone[i] = i + 1;
            }
        }
        RandomReset();
    }
    /// <summary>initMapObj / randomReset: each drum on a random face, nothing held.</summary>
    public void RandomReset()
    {
        var rng = new Random();
        for (int i = 0; i < 3; i++) { _ang[i] = FaceDeg * rng.Next(0, 8); _held[i] = false; _toTarget[i] = false; _spd[i] = 0f; }
        ApplyDrums();
    }
    /// <summary>moveStart: the three drums spinning and HELD (-1 / -0.8 / +1 x 2.0 deg a tick); the first forced stop armed.</summary>
    public void MoveStart()
    {
        _firstStopPending = true; _rolling = true;
        for (int i = 0; i < 3; i++)
        {
            _held[i] = true; _toTarget[i] = false;
            float s = i == 0 ? -1f : (i == 1 ? -0.8f : 1f);
            _spd[i] = s * StartSpeed;
        }
    }
    public void StopAll() { for (int i = 0; i < 3; i++) { _spd[i] = 0f; _held[i] = false; _toTarget[i] = false; } }
    /// <summary>TTelesaSlot::forceStopSlot(idx): the target decided (FirstHitCollectRate -> FRUIT / COIN by Mario's HP; else the drum's own forecast; the
    /// re-reads: last round's result -> JACKPOT; COIN with no coin rounds -> ENEMY / Mario at 6+ -> JACKPOT), the drum held to it or released.</summary>
    public void ForceStopSlot(int idx, float firstHitCollectRate, int marioHp, int lastResult, int coinRoundsLeft, Random rng)
    {
        if (!_firstStopPending) return;
        float rate = marioHp == 1 ? 0.9f : firstHitCollectRate;
        if (rng.NextDouble() <= rate)
        {
            _target = marioHp <= 3 ? Coin : Fruit;
            _toTarget[idx] = true;
        }
        else { _target = ForecastResult(idx); _held[idx] = false; }
        if (_target == lastResult) _target = Jackpot;
        if (_target == Coin) { if (coinRoundsLeft == 0) _target = Enemy; else if (marioHp >= 6) _target = Jackpot; }
        if (ForcedResult != int.MinValue) { _target = ForcedResult; _toTarget[idx] = true; _held[idx] = true; }   // a drive's override
        _firstStopPending = false;
    }
    /// <summary>isRollDrum: a drum still held -> true; else the rolling flag off.</summary>
    public bool IsRollDrum() { if (_held[0] || _held[1] || _held[2]) return true; _rolling = false; return false; }
    public int SlotResult()
    {
        int r = DrumResult(0);
        for (int i = 1; i < 3; i++) if (DrumResult(i) != r) return Mixed;
        return r;
    }
    public int DrumResult(int i) => ResultFromAng(_ang[i]);
    public static int ResultFromAng(float ang)
    {
        if (ang < 44f) return 0; if (ang < 89f) return 1; if (ang < 134f) return 3; if (ang < 179f) return 2;
        if (ang < 224f) return 0; if (ang < 269f) return 1; if (ang < 314f) return 3; return 2;
    }
    /// <summary>getForcastResult: the face the drum would stop on if released now (the decel to the creep, then the next 45-deg stop).</summary>
    public int ForecastResult(int i)
    {
        float ang = _ang[i], cur = _spd[i];
        int guard = 0;
        while (guard++ < 200000)
        {
            if (MathF.Abs(cur) > CreepSpeed)
            {
                ang += cur; cur += cur > 0f ? -Decel : Decel;
                if (ang >= 360f) ang -= 360f; if (ang <= 0f) ang += 360f;
            }
            else
            {
                if (cur == 0f) break;
                ang += cur;
                if (ang >= 360f) ang -= 360f; if (ang <= 0f) ang += 360f;
                if ((int)MathF.Abs(ang) % FaceDeg == 0) break;
            }
        }
        return ResultFromAng(FaceDeg * (int)(ang / FaceDeg));
    }
    /// <summary>TTelesaSlot::moveObject, one game tick: the to-target drums released when their forecast is the target; the held drums spin flat, the
    /// released ones decelerate and stop on a face (SLT_STOP), each stop holding the still-held drums to the target with HitCollectRate; all stopped
    /// -> the boss's fanfale.</summary>
    public void TickSlot(float hitCollectRate, Random rng)
    {
        for (int i = 0; i < 3; i++)
        {
            if (_toTarget[i] && ForecastResult(i) == _target) { _held[i] = false; _toTarget[i] = false; }
            float s = _spd[i];
            if (s == 0f) continue;
            if (MathF.Abs(s) > CreepSpeed)
            {
                _ang[i] += s;
                if (!_held[i]) _spd[i] += s > 0f ? -Decel : Decel;
                if (_ang[i] >= 360f) _ang[i] -= 360f; if (_ang[i] <= 0f) _ang[i] += 360f;
            }
            else
            {
                _ang[i] += s;
                if (_ang[i] >= 360f) _ang[i] -= 360f; if (_ang[i] <= 0f) _ang[i] += 360f;
                if (_held[i]) continue;
                if ((int)MathF.Abs(_ang[i]) % FaceDeg != 0) continue;
                _ang[i] = FaceDeg * (int)(_ang[i] / FaceDeg);
                _spd[i] = 0f;
                if (StopSound != null && StopSound.Stream != null) StopSound.Play();
                for (int j = 0; j < 3; j++)
                    if (_held[j]) { if (rng.NextDouble() <= hitCollectRate) _toTarget[j] = true; else _held[j] = false; }
                bool all = _spd[0] == 0f && _spd[1] == 0f && _spd[2] == 0f;
                if (all) Boss?.Fanfale();
            }
        }
        bool rollingNow = _spd[0] != 0f || _spd[1] != 0f || _spd[2] != 0f;
        if (rollingNow && SpinSound != null && SpinSound.Stream != null && (_spinTickToggle++ & 1) == 1 && !SpinSound.Playing) SpinSound.Play();
    }
    /// <summary>The drum joints rolled about their X by the reels' angles (partsRollCallback's MsMtxSetRotRPH(angX, 0, 0)).</summary>
    public void ApplyDrums()
    {
        if (_skel == null) return;
        for (int i = 0; i < 3; i++)
        {
            if (_drumBone[i] < 0) continue;
            Transform3D rest = _skel.GetBoneRest(_drumBone[i]);
            _skel.SetBonePose(_drumBone[i], new Transform3D(rest.Basis * new Basis(Vector3.Right, Mathf.DegToRad(_ang[i])), rest.Origin));
        }
    }
    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint()) return;
        int n = SmsUnits.TicksFor(delta, ref _accum);
        if (Boss == null) { for (int k = 0; k < n; k++) TickSlot(1f, _rng); }
        ApplyDrums();
    }
    private readonly Random _rng = new Random();
}
