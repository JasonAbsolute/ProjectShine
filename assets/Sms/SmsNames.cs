using System.Collections.Generic;
using Godot;

/// <summary>227: Godot names made ONCE. A C# string handed to a Godot call that wants a StringName (an input action, a group, a metadata key, an
/// animation, a state machine's state) or a NodePath (GetNode) is converted on every call -- a new finalizable wrapper the .NET collector has to
/// carry to gen1 before it can go (226: the 41-45 ms pauses). The port made hundreds a second of them in Mario's and the camera's ticks. Of / Path
/// hand back the same StringName / NodePath for the same string, from one dictionary (a lookup, no allocation). Main-thread only, as the callers are.</summary>
public static class SmsNames
{
    private static readonly Dictionary<string, StringName> _names = new();
    private static readonly Dictionary<string, NodePath> _paths = new();

    public static StringName Of(string s)
    {
        if (!_names.TryGetValue(s, out var n)) { n = new StringName(s); _names[s] = n; }
        return n;
    }

    public static NodePath Path(string s)
    {
        if (!_paths.TryGetValue(s, out var p)) { p = new NodePath(s); _paths[s] = p; }
        return p;
    }

    /// <summary>For the drives: how many names and paths are held.</summary>
    public static int Count => _names.Count + _paths.Count;
}

/// <summary>227: the keys of a physics query's result dictionary, as Variants made once. `hit["position"]` turns the C# string into a Variant on
/// every read -- a native string and a finalizable Disposer each time; a Variant kept in a static is copied by value, nothing made.</summary>
public static class SmsKeys
{
    public static readonly Variant Position = "position", Normal = "normal", Collider = "collider", ColliderId = "collider_id", Rid = "rid", Point = "point", Shape = "shape";
}

/// <summary>227: a typed Godot array let go at once. Array&lt;T&gt; is not IDisposable; the untyped Array under it is (and is the finalizable one).</summary>
public static class SmsFree
{
    public static void Array<[MustBeVariant] T>(Godot.Collections.Array<T> a) => ((Godot.Collections.Array)a)?.Dispose();
}
