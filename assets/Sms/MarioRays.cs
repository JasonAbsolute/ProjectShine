using Godot;

/// <summary>227: the rays Mario casts -- several a tick (the floor under the feet, the water, the surface tag, the ledges, the walls, the steps)
/// -- without the garbage each one made: a PhysicsRayQueryParameters3D, an exclude list and a result dictionary, three finalizable Godot wrappers
/// the .NET collector had to carry to gen1 (226: the 41-45 ms pauses). One query object is re-used (From / To and the options set per cast; the
/// exclude list of his own body made once), and the result dictionaries go into a ring of 64 and are disposed 64 casts later -- a tick casts under
/// a dozen and no caller keeps a result past its own frame -- so they die young and finalizer-free. Every cast in Mario.cs goes through here; the
/// water check's point query is the same object each tick too. Below, the other per-tick wrappers his loops made: the slide collisions, BaseSM's
/// state name, the floor's surface tag.</summary>
public partial class Mario
{
    private PhysicsRayQueryParameters3D _smsRayQ;
    private Godot.Collections.Array<Rid> _smsRaySelf, _smsRayExcl;
    private PhysicsPointQueryParameters3D _smsPointQ;
    private readonly Godot.Collections.Dictionary[] _smsRayRing = new Godot.Collections.Dictionary[64];
    private int _smsRayRingAt;

    /// <summary>The shared query, set for this cast: from, to, the mask (his own CollisionMask unless given), bodies yes, areas no unless asked,
    /// HitFromInside off unless asked, his own body excluded (or the list given).</summary>
    private PhysicsRayQueryParameters3D SmsRayQuery(Vector3 from, Vector3 to, uint? mask = null, bool areas = false, bool hitFromInside = false, Godot.Collections.Array<Rid> exclude = null)
    {
        if (_smsRayQ == null) { _smsRayQ = new PhysicsRayQueryParameters3D(); _smsRaySelf = new Godot.Collections.Array<Rid> { GetRid() }; }
        _smsRayQ.From = from; _smsRayQ.To = to; _smsRayQ.CollisionMask = mask ?? CollisionMask;
        _smsRayQ.CollideWithBodies = true; _smsRayQ.CollideWithAreas = areas; _smsRayQ.HitFromInside = hitFromInside;
        _smsRayQ.Exclude = exclude ?? _smsRaySelf;
        return _smsRayQ;
    }

    /// <summary>A second exclude list, re-used: his own body, plus whatever the caller adds before the cast (what he holds, the trunk he hugs).</summary>
    private Godot.Collections.Array<Rid> SmsRayExcludeSelf()
    {
        _smsRayExcl ??= new Godot.Collections.Array<Rid>();
        _smsRayExcl.Clear(); _smsRayExcl.Add(GetRid());
        return _smsRayExcl;
    }

    /// <summary>The cast. The caller reads the result at once; its dictionary is disposed 64 casts on.</summary>
    private Godot.Collections.Dictionary SmsIntersect(PhysicsRayQueryParameters3D q)
    {
        var d = GetWorld3D().DirectSpaceState.IntersectRay(q);
        _smsRayRing[_smsRayRingAt]?.Dispose(); _smsRayRing[_smsRayRingAt] = d; _smsRayRingAt = (_smsRayRingAt + 1) & 63;
        return d;
    }

    /// <summary>The shared point query (the water check): areas only, every layer, at the point given.</summary>
    private PhysicsPointQueryParameters3D SmsPointQuery(Vector3 at)
    {
        _smsPointQ ??= new PhysicsPointQueryParameters3D { CollideWithAreas = true, CollideWithBodies = false, CollisionMask = uint.MaxValue };
        _smsPointQ.Position = at;
        return _smsPointQ;
    }

    /// <summary>GetSlideCollision(i) with the last one's wrapper disposed first. The engine re-uses its cached KinematicCollision3D only while
    /// nothing else holds it; a live C# wrapper does, so every read made a new native object and a finalizable wrapper (several a tick: the
    /// surface, the ride, the wall, the ceiling). Disposed at once, the cache serves and the wrapper dies young. Callers read it within their loop.</summary>
    private KinematicCollision3D _smsSlideLast;
    private KinematicCollision3D SmsSlideHit(int i)
    {
        _smsSlideLast?.Dispose();
        _smsSlideLast = GetSlideCollision(i);
        return _smsSlideLast;
    }

    /// <summary>BaseSM's current state as a string: the StringName the playback hands back disposed once read (it was left to the finalizer).</summary>
    private string SmsStateNode()
    {
        using var sn = _sm.GetCurrentNode();
        return sn.ToString();
    }

    /// <summary>The ground class of a floor piece, read once per piece and remembered (SmsSurfaceTagOf walks three ancestors' metadata lists and
    /// groups -- two Godot arrays and a wrapper per name, every frame on a floor without the `sms_surface` meta). The level's sms_ tags are set
    /// in the scene; nothing in the project changes one after a node is made, so the first read stands. Trimmed and lower-cased, as the readers had it.</summary>
    private readonly System.Collections.Generic.Dictionary<ulong, string> _smsSurfaceTags = new();
    private string SmsSurfaceTag(Node hit)
    {
        ulong id = hit.GetInstanceId();
        if (!_smsSurfaceTags.TryGetValue(id, out string tag)) { tag = SmsSurfaceTagOf(hit).Trim().ToLowerInvariant(); _smsSurfaceTags[id] = tag; }
        return tag;
    }

    /// <summary>The ring emptied and the shared objects let go (a scene change frees the world first; they are remade on the next cast).</summary>
    private void SmsRaysDispose()
    {
        for (int i = 0; i < _smsRayRing.Length; i++) { _smsRayRing[i]?.Dispose(); _smsRayRing[i] = null; }
        _smsRayQ?.Dispose(); _smsRayQ = null; SmsFree.Array(_smsRaySelf); _smsRaySelf = null; SmsFree.Array(_smsRayExcl); _smsRayExcl = null;
        _smsPointQ?.Dispose(); _smsPointQ = null;
        _smsSlideLast?.Dispose(); _smsSlideLast = null;
    }

    public override void _ExitTree() => SmsRaysDispose();
}
