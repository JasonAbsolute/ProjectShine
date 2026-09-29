using Godot;
using System;

/// <summary>
/// Sunshine's nozzle box: TNozzleBox (MoveBG/Item.cpp 1181-1338, MapObjInit.cpp NozzleBox_data), the crate that holds a spare nozzle.
/// The model is dolpic0/mapobj/nozzlebox.bmd (205.6 units a side, its origin at the base) with nozzlebox.bmt's J_nozzleBox face --
/// the FLUDD logo and four screws on a white panel -- tinted per nozzle through GX_TEVREG1 (load 1272-1320: hover 0000FF, rocket
/// FF0000, turbo 5A5A78): a blue, a red and a grey box. Mario landing on it (HIT_MESSAGE_TRAMPLE from above, not a belly slide)
/// bounces him (HIT_MESSAGE_ATTACK back: the stomp) and breaks the box (receiveMessage 1210): the item is thrown out of the box's front
/// (throwObjToFront: 50 units up the base, along the box's +Z at the level's speed, with the level's vertical speed -- 20 when unset),
/// the crash model nozzleboxcrash.bmd plays its BCK (49 frames) and the box is dead. When the item is taken every dead box of that
/// nozzle comes back at once with a smoke puff (TItemManager::resetNozzleBoxesModel 16) -- so a box can be used again.
/// Not here: the "valid" flag (a rocket / turbo box stays shut until the nozzle right is won: every box is valid), the hint balloon,
/// the SEs, the smoke puff (a short scale-in stands in). The throw speeds are level data in the game; here they are the node's.
/// Place one in a level: a StaticBody3D with this script (models/Nozzles/NozzleBox.tscn), its Nozzle set; it builds its own
/// collision, model and stomp zone when the game runs. A tool script: in the editor it shows the box's tinted model (a child the
/// scene does not save) and retints it when Nozzle changes, so a level shows its boxes.
/// </summary>
[Tool]
public partial class SmsNozzleBox : StaticBody3D
{
    public enum BoxNozzle { Hover = 1, Rocket = 2, Turbo = 3 } // the port's SmsNozzle indices

    private BoxNozzle _nozzle = BoxNozzle.Hover;
    [Export] public BoxNozzle Nozzle
    {
        get => _nozzle;
        set
        {
            _nozzle = value;
            if (Engine.IsEditorHint() && IsInsideTree())
                EditorPreview(true);
        }
    }
    /// <summary>throwObjToFront's speed along the box's front, units a 30 fps frame (the level's parameter x 0.02 in the game).</summary>
    [Export] public float ThrowSpeed = 2.0f;
    /// <summary>throwObjToFront's vertical speed, units a 30 fps frame (20 when the level leaves it unset; 10 arcs a metre or two).</summary>
    [Export] public float ThrowVerticalSpeed = 10.0f;

    public const float SizeUnits = 205.6f;
    private const string ModelDir = "res://models/Koopa/scene/dolpic0/mapobj/";
    private const string FacePng = "res://models/Koopa/scene/dolpic0/mapobj/nozzlebox/J_nozzleBox.png";

    private Node3D _model, _crash;
    private AnimationPlayer _crashAnim;
    private CollisionShape3D _shape;
    private Area3D _stomp;
    private bool _broken;
    private float _respawnScale = 1f;
    private static Texture2D _faceTex;

    public static Color TintFor(BoxNozzle n) => n switch
    {
        BoxNozzle.Rocket => new Color(1f, 0f, 0f),
        BoxNozzle.Turbo => new Color(0x5A / 255f, 0x5A / 255f, 0x78 / 255f),
        _ => new Color(0f, 0f, 1f),
    };

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            EditorPreview(false);
            return;
        }
        CollisionLayer = 1;
        CollisionMask = 0;
        float s = 1f / SmsUnits.UnitsPerMetre;
        float size = SizeUnits * s;
        _shape = new CollisionShape3D { Name = "Shape", Shape = new BoxShape3D { Size = new Vector3(size, size, size) }, Position = new Vector3(0f, size * 0.5f, 0f) };
        AddChild(_shape);
        // the trample: Mario's body coming down into the zone over the lid (Gooble.cs does the same with its StompZone)
        _stomp = new Area3D { Name = "StompZone", Monitoring = true, Monitorable = false };
        // a metre over the lid: he enters it a frame or more before he lands, while his fall is still a fall (a hip drop covers 0.7 m a frame)
        _stomp.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(size * 0.9f, 1.0f, size * 0.9f) }, Position = new Vector3(0f, size + 0.5f, 0f) });
        AddChild(_stomp);
        var mat = BoxMaterial(TintFor(Nozzle));
        _model = SmsGlb.Instantiate(ModelDir + "nozzlebox/nozzlebox");
        if (_model != null)
        {
            _model.Name = "Model";
            _model.Scale = new Vector3(s, s, s);
            AddChild(_model);
            SmsGlb.OverrideMaterials(_model, mat);
        }
        else
            GD.PushWarning("[SMS nozzle box] nozzlebox.glb not found under " + ModelDir);
        _crash = SmsGlb.Instantiate(ModelDir + "nozzleboxcrash/nozzleboxcrash");
        if (_crash != null)
        {
            _crash.Name = "Crash";
            _crash.Scale = new Vector3(s, s, s);
            _crash.Visible = false;
            AddChild(_crash);
            SmsGlb.OverrideMaterials(_crash, mat);
            _crashAnim = SmsGlb.FindChild<AnimationPlayer>(_crash);
        }
    }

    /// <summary>The editor's stand-in for the model the game builds: the box, tinted for the nozzle, as an unsaved child.</summary>
    private void EditorPreview(bool rebuild)
    {
        var old = GetNodeOrNull<Node3D>("EditorPreview");
        if (old != null)
        {
            if (!rebuild)
                return;
            RemoveChild(old);
            old.QueueFree();
        }
        var model = SmsGlb.Instantiate(ModelDir + "nozzlebox/nozzlebox");
        if (model == null)
            return;
        model.Name = "EditorPreview";
        float s = 1f / SmsUnits.UnitsPerMetre;
        model.Scale = new Vector3(s, s, s);
        AddChild(model);
        SmsGlb.OverrideMaterials(model, BoxMaterial(TintFor(_nozzle)));
    }

    private static StandardMaterial3D BoxMaterial(Color tint)
    {
        if (_faceTex == null)
            _faceTex = SmsGlb.LoadTexture(FacePng);
        return new StandardMaterial3D
        {
            ResourceName = "nozzle box (J_nozzleBox x TEVREG1)",
            AlbedoTexture = _faceTex,
            AlbedoColor = tint,
            Roughness = 1f,
            Metallic = 0f,
            CullMode = BaseMaterial3D.CullModeEnum.Back,
        };
    }

    private bool _respawnPending;

    private bool MarioInside(Mario m)
    {
        float half = SizeUnits / SmsUnits.UnitsPerMetre * 0.5f + 0.4f;
        Vector3 d = m.GlobalPosition - GlobalPosition;
        return MathF.Abs(d.X) < half && MathF.Abs(d.Z) < half && d.Y > -0.5f && d.Y < SizeUnits / SmsUnits.UnitsPerMetre;
    }

    public string SmsDebug() => $"broken {_broken}, pending {_respawnPending}, zone monitoring {_stomp?.Monitoring}, overlaps {(_stomp != null && _stomp.Monitoring ? _stomp.GetOverlappingBodies().Count : -1)}";

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint())
            return;
        if (_respawnPending)
        {
            Respawn();
            return;
        }
        if (_broken || !_stomp.Monitoring)
            return;
        // polled, not body_entered: a jump from beside the box puts him in the zone while he still rises, and the landing that follows
        // would be no new entry. His velocity BEFORE this frame's MoveAndSlide is the fall the game's TRAMPLE sees (the landing on the
        // lid has zeroed the body's own); -2.5 m/s keeps a standing Mario (the ground hold's -2) from re-opening a box that comes back
        // under him. Not a belly slide / dive (SMS_IsMarioHeadSlideAttack).
        foreach (Node3D body in _stomp.GetOverlappingBodies())
        {
            if (body is Mario mario && mario.SmsPreMoveVelocity.Y < -2.5f && mario.SmsCanTrampleBox())
            {
                Break(mario);
                break;
            }
        }
    }

    /// <summary>receiveMessage(TRAMPLE): the stomp back to Mario, the item out of the front, the crash BCK, the box dead.</summary>
    private void Break(Mario mario)
    {
        _broken = true;
        mario.StompBounce();
        _shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        _stomp.SetDeferred(Area3D.PropertyName.Monitoring, false);
        if (_model != null) _model.Visible = false;
        if (_crash != null)
        {
            _crash.Visible = true;
            if (_crashAnim != null && _crashAnim.HasAnimation("nozzleboxcrash"))
            {
                _crashAnim.Stop();
                _crashAnim.Play("nozzleboxcrash");
                _crashAnim.AnimationFinished += OnCrashFinished;
            }
            else
                _crash.Visible = false;
        }
        _debug = mario.SmsDebugPrint;
        CallDeferred(MethodName.SpawnItem); // a new body during the area's own signal: after the physics step
    }

    private bool _debug;

    /// <summary>throwObjToFront(item, 50, speed, vertical): from 50 units up the base, along the box's front (its +Z), the level's speeds.</summary>
    private void SpawnItem()
    {
        var item = new SmsNozzleItem { Nozzle = Nozzle, Box = this };
        GetParent().AddChild(item);
        float s = 1f / SmsUnits.UnitsPerMetre;
        item.GlobalPosition = GlobalPosition + new Vector3(0f, 50f * s, 0f);
        Vector3 front = GlobalTransform.Basis.Z.Normalized();
        item.SetVelocityUnitsPerFrame(front * ThrowSpeed + Vector3.Up * ThrowVerticalSpeed);
        if (_debug)
            GD.Print($"[SMS nozzle box] {Nozzle} box trampled: the item out along {front} at {ThrowSpeed} u/frame, up {ThrowVerticalSpeed}; the crash plays");
    }

    private void OnCrashFinished(StringName anim)
    {
        if (_crashAnim != null)
            _crashAnim.AnimationFinished -= OnCrashFinished;
        if (_crash != null)
            _crash.Visible = false; // makeObjDead
    }

    /// <summary>resetNozzleBoxesModel: the item of this nozzle was taken -- every dead box of the kind is whole again.</summary>
    public void Respawn()
    {
        if (!_broken)
            return;
        // never back onto Mario: the game's boxes respawn where the item lands clear of them; here the box waits until he has stepped out
        if (GetTree().GetFirstNodeInGroup("player") is Mario m && MarioInside(m))
        {
            _respawnPending = true;
            return;
        }
        _respawnPending = false;
        _broken = false;
        if (_crashAnim != null && _crashAnim.IsPlaying())
        {
            _crashAnim.AnimationFinished -= OnCrashFinished;
            _crashAnim.Stop();
        }
        if (_crash != null) _crash.Visible = false;
        if (_model != null) _model.Visible = true;
        _shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
        _stomp.SetDeferred(Area3D.PropertyName.Monitoring, true);
        _respawnScale = 0.3f; // the smoke puff's stand-in: a short scale-in
    }

    public override void _Process(double delta)
    {
        if (_respawnScale < 1f && _model != null)
        {
            _respawnScale = MathF.Min(1f, _respawnScale + (float)delta * 5f);
            float s = _respawnScale / SmsUnits.UnitsPerMetre;
            _model.Scale = new Vector3(s, s, s);
        }
    }

    /// <summary>Every box of a nozzle in the tree (the item manager's loop over the boxes).</summary>
    public static void RespawnAll(Node anyNode, BoxNozzle nozzle)
    {
        foreach (Node n in anyNode.GetTree().GetNodesInGroup("sms_nozzle_box"))
            if (n is SmsNozzleBox b && b.Nozzle == nozzle)
                b.Respawn();
    }

    public override void _EnterTree()
    {
        if (!Engine.IsEditorHint())
            AddToGroup("sms_nozzle_box");
    }
}

/// <summary>Model files for the SMS objects: the editor's import when it has run, the glTF loader straight off the disk when it has not.</summary>
public static class SmsGlb
{
    public static Node3D Instantiate(string basePath)
    {
        string p = basePath + ".glb";
        if (ResourceLoader.Exists(p))
            return ResourceLoader.Load<PackedScene>(p)?.Instantiate<Node3D>();
        string abs = ProjectSettings.GlobalizePath(p);
        if (!FileAccess.FileExists(abs))
            return null;
        var doc = new GltfDocument();
        var state = new GltfState();
        if (doc.AppendFromFile(abs, state) != Error.Ok)
            return null;
        return doc.GenerateScene(state) as Node3D;
    }

    public static Texture2D LoadTexture(string path)
    {
        if (ResourceLoader.Exists(path))
            return ResourceLoader.Load<Texture2D>(path);
        var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        return img == null || img.IsEmpty() ? null : ImageTexture.CreateFromImage(img);
    }

    public static T FindChild<T>(Node root) where T : Node
    {
        if (root is T t)
            return t;
        foreach (Node c in root.GetChildren())
        {
            var r = FindChild<T>(c);
            if (r != null)
                return r;
        }
        return null;
    }

    public static void OverrideMaterials(Node root, Material mat)
    {
        if (root is MeshInstance3D mi && mi.Mesh != null)
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                mi.SetSurfaceOverrideMaterial(i, mat);
        foreach (Node c in root.GetChildren())
            OverrideMaterials(c, mat);
    }
}
