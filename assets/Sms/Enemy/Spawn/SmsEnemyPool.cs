using System.Collections.Generic;
using Godot;

/// <summary>
/// A spawner's enemies -- TEnemyManager's pool as the generators and launchers see it (Enemy/enemymanager.cpp 405-436; pass 126). The game's
/// manager makes mSLInstanceNum objects at load, all LIVE_FLAG_DEAD, and a TGenerator / TOneShotGenerator / TCommonLauncher takes one with
/// getFarOutEnemy() and resetSRTV()s it at its own point. Here each spawner owns its pool: <see cref="Fill"/> instantiates <c>count</c> of the
/// enemy scene (any <see cref="SmsSmallEnemy"/> kind) beside the spawner, <see cref="SmsSmallEnemy.Pooled"/> and dead; <see cref="FarOut"/> hands
/// one back by the game's rule. The scene's placed enemies are not part of any pool (they are their own launchers, pass 125).
/// </summary>
public sealed class SmsEnemyPool
{
    private readonly List<SmsSmallEnemy> _bodies = new();
    public IReadOnlyList<SmsSmallEnemy> Bodies => _bodies;
    public int Count => _bodies.Count;
    public int Alive { get { int n = 0; foreach (var b in _bodies) if (GodotObject.IsInstanceValid(b) && !b.IsDead) n++; return n; } }
    public int Dead { get { int n = 0; foreach (var b in _bodies) if (GodotObject.IsInstanceValid(b) && b.IsDead) n++; return n; } }

    /// <summary>createEnemies: <c>count</c> bodies of <c>scene</c> as children of <c>parent</c> (the spawner's own parent, so the spawner's transform
    /// is not theirs), pooled, dead at <c>at</c>. Returns how many were made (0 with a warning when the scene is not an SmsSmallEnemy kind).</summary>
    public int Fill(Node parent, PackedScene scene, int count, Vector3 at, bool debugPrint, string who)
    {
        if (parent == null || scene == null || count <= 0) return 0;
        int made = 0;
        for (int i = 0; i < count; i++)
        {
            Node n = scene.Instantiate();
            if (n is not SmsSmallEnemy e)
            {
                if (i == 0) GD.PushWarning($"[SMS spawner] {who}: {scene.ResourcePath.GetFile()} is not an SmsSmallEnemy kind -- nothing to launch");
                n.Free();
                return 0;
            }
            e.Pooled = true; e.RelaunchTicks = -1; e.DebugPrint = debugPrint;
            e.Name = $"{who}_{scene.ResourcePath.GetFile().GetBaseName()}{i + 1}";
            e.Position = parent is Node3D p3 ? p3.ToLocal(at) : at;
            parent.AddChild(e);
            e.SmsSpawnDead();
            _bodies.Add(e);
            made++;
        }
        return made;
    }

    /// <summary>TEnemyManager::getFarOutEnemy: a dead body first; else, of the live ones the game would have clipped out (here: farther than
    /// <c>recycleDistance</c> metres from Mario -- the port does not clip), the nearest of them to him. Null when every body is live and near.</summary>
    public SmsSmallEnemy FarOut(Vector3 marioPos, float recycleDistance)
    {
        SmsSmallEnemy best = null; float bestD = float.MaxValue;
        foreach (var b in _bodies)
        {
            if (!GodotObject.IsInstanceValid(b)) continue;
            if (b.IsDead) return b;
            float d = b.GlobalPosition.DistanceTo(marioPos);
            if (d >= recycleDistance && d < bestD) { best = b; bestD = d; }
        }
        return best;
    }

    /// <summary>killChildren: every body dead (a spawner freed with its level).</summary>
    public void KillAll() { foreach (var b in _bodies) if (GodotObject.IsInstanceValid(b)) b.SmsKillByDemo(); }
}
