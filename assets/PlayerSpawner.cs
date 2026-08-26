using Godot;
using System.Collections.Generic;

/// <summary>
/// Spawns whichever character the player picked, at this node's transform.
///
/// Levels no longer hard-code a character instance. Put one of these where the
/// player should start; at runtime it instantiates the selection from GameData
/// and hands over any children it was given (ShineTimer, HUD hooks) so their
/// relative NodePaths still resolve.
///
/// The spawned node is renamed to <see cref="SpawnedNodeName"/> — "Mario" by
/// default — and lands at "&lt;spawner&gt;/Mario", so a level using Level.cs must
/// point its MarioPath at that (e.g. "PlayerSpawner/Mario").
/// Node lookups elsewhere use the "player" group, which the character scenes
/// already carry, so those are unaffected either way.
///
/// Godot readies children before parents, so this runs before Level._Ready and
/// the player is present by the time the level looks for it.
/// </summary>
public partial class PlayerSpawner : Node3D
{
    /// <summary>
    /// Used when there's no selection — i.e. running the level directly from the
    /// editor instead of coming through Player Select. Point it at Mario.tscn.
    /// </summary>
    [Export] public PackedScene FallbackCharacter;

    /// <summary>Name given to the spawned node. Level.cs's MarioPath expects "Mario".</summary>
    [Export] public string SpawnedNodeName = "Mario";

    /// <summary>
    /// Extra yaw in degrees applied on top of this node's authored rotation.
    /// Normally 0 — place the spawner facing the way the character should start.
    /// </summary>
    [Export] public float ExtraFacingDegrees = 0f;

    /// <summary>The character that actually got spawned, once _Ready has run.</summary>
    public Node3D SpawnedPlayer { get; private set; }

    public override void _Ready()
    {
        PackedScene scene = GameData.Instance?.SelectedCharacterScene ?? FallbackCharacter;

        if (scene == null)
        {
            GD.PushError(
                "PlayerSpawner: nothing to spawn — no character selected and no "
                    + "FallbackCharacter set. The level will have no player."
            );
            return;
        }

        if (scene.Instantiate() is not Node3D player)
        {
            GD.PushError($"PlayerSpawner: '{scene.ResourcePath}' is not a Node3D.");
            return;
        }

        player.Name = SpawnedNodeName;

        // Add the player as OUR child, not as a sibling. During ready propagation
        // the parent is blocked ("Parent node is busy setting up children"), so
        // AddChild on it fails — and call_deferred would push the spawn past
        // Level._Ready, which needs the player to already exist. We aren't
        // blocked ourselves, so adopting the child here works synchronously.
        //
        // The player therefore lives at <spawner>/<SpawnedNodeName>, and inherits
        // this node's transform — which is the spawn point — so it needs no
        // transform of its own.
        // Snapshot BEFORE adopting the player, or the player ends up in its own
        // handover list and we try to reparent it into itself.
        var handover = new List<Node>(GetChildren());

        AddChild(player);

        // Hand our authored rotation down to the character and keep only the
        // position ourselves — reproducing the node graph levels had before
        // spawners existed, where the Mario instance sat directly under the level
        // root carrying its own rotation.
        //
        // This matters because Mario.cs composes movement as
        //     Transform.Basis * stick, then .Rotated(Up, springArmPivot.Rotation.Y)
        // (Mario.cs:1777). That is the character's LOCAL basis plus the camera's
        // LOCAL yaw, and the two only add up to the camera's world yaw while the
        // character's parent is unrotated. Any rotation left on this node leaks
        // in as a constant offset, which is why a 180 here comes out as inverted
        // controls rather than as a turned character.
        Basis authored = Transform.Basis;
        Transform = new Transform3D(Basis.Identity, Transform.Origin);

        if (ExtraFacingDegrees != 0f)
            authored *= Basis.FromEuler(new Vector3(0f, Mathf.DegToRad(ExtraFacingDegrees), 0f));

        player.Transform = new Transform3D(authored, Vector3.Zero);

        // Hand over anything parented to the spawner in the editor. Only do this
        // for nodes that don't resolve NodePaths in their own _Ready — those run
        // BEFORE this spawner does, so they'd resolve against the old parent and
        // cache the wrong result. Put those at the level root, declared after the
        // spawner, instead.
        foreach (var child in handover)
        {
            RemoveChild(child);
            player.AddChild(child);
        }

        SpawnedPlayer = player;
    }
}
