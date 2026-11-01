using Godot;

/// <summary>
/// TTamaNokoFlower (Enemy/tamaNoko.cpp): the sunflower that grows where a Snooza Koopa died (SMS pass 145). A TSharedParts of the dead body in the
/// game (tamaflower_model1 at the body's point, grow1 once); when grow1 is about to end: reviveOneSunflower (Pinna's sunflower event, not ported),
/// then FIVE coins made 350 u out from the point at 72-deg steps, each flying outward (dir x 4, 20, dir x 4) and binding (they fall). Here a node of
/// its own under the level (the body is freed), the glb's AnimationPlayer playing grow1; its end spawns the coins through an SmsDropTable.
/// </summary>
public partial class SmsTamaFlower : Node3D
{
    /// <summary>The coin (res://yellow_coin.tscn); empty = no coins.</summary>
    [Export] public PackedScene CoinScene;
    /// <summary>The coins: five, 350 u out, 72 deg apart, flying outward at 4 u a tick and 20 u a tick up (the game's).</summary>
    [Export] public int CoinCount = 5;
    [Export] public float CoinRadiusUnits = 350f, CoinOutSpeed = 4f, CoinUpSpeed = 20f;
    /// <summary>MSD_SE_EN_COMMON_SMOKE / the bas table's grow sound: a slot.</summary>
    [Export] public AudioStreamPlayer3D GrowSound;
    public bool Grown { get; private set; }
    public int CoinsMade { get; private set; }
    private AnimationPlayer _anim; private bool _started;
    private const string Clip = "tamaflower_grow1";

    public override void _Ready()
    {
        _anim = FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        if (_anim != null && _anim.HasAnimation(Clip))
        {
            var a = _anim.GetAnimation(Clip); a.LoopMode = Animation.LoopModeEnum.None;
            _anim.Play(Clip); _anim.Seek(0f, true);
            _anim.AnimationFinished += OnFinished;
            _started = true;
        }
        else { GD.PushWarning($"[SMS tamaflower] {GetPath()}: no '{Clip}' clip under the scene -- the coins come at once"); CallDeferred(nameof(Burst)); }
        GrowSound?.Play();
    }
    private void OnFinished(StringName which) { if (which == Clip) Burst(); }
    /// <summary>The five coins, ItemManager::makeObjAppear at the point + 350 u along each direction, the velocity (dir x 4, 20, dir x 4).</summary>
    private void Burst()
    {
        if (Grown) return;
        Grown = true;
        if (CoinScene == null || !IsInsideTree()) return;
        var table = new SmsDropTable();
        Node parent = GetParent() ?? this;
        for (int i = 0; i < CoinCount; i++)
        {
            float a = Mathf.DegToRad((i + 1) * (360f / CoinCount));
            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 at = GlobalPosition + dir * (CoinRadiusUnits / SmsUnits.UnitsPerMetre);
            var node = table.SpawnItem(CoinScene, parent, at, new Vector3(dir.X * CoinOutSpeed, CoinUpSpeed, dir.Z * CoinOutSpeed), 0, this);
            if (node != null) CoinsMade++;
        }
    }
}
