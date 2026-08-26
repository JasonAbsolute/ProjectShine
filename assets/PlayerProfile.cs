using Godot;

/// <summary>
/// Stores everything that makes one playable character look different from
/// another. Mario, Luigi and Piantissimo all share ONE player scene, ONE
/// Mario.cs and ONE AnimationTree — only the visual body is swapped, driven by
/// one of these resources.
///
/// Create a .tres file per character/skin:
///   Right-click in FileSystem → New Resource → PlayerProfile
///
/// Leave any field null to keep Mario's default for that slot, so a profile can
/// be a full character (mesh + skin + textures) or just a palette swap.
/// </summary>
[GlobalClass]
public partial class PlayerProfile : Resource
{
    [Export] public string ProfileName = "Player 1";

    // ---------------------------------------------------------------- body --

    /// <summary>
    /// The imported character model, e.g. res://models/Luigi/Godot/Luigi.gltf.
    /// This is the easy path: drag the .gltf/.glb here and the mesh and its
    /// matching Skin are lifted out of it together, so they can never be
    /// mismatched. The model must be rigged to the SAME 29-bone SMS skeleton
    /// (mdl1 / center / jnt_waist / ...) because the shared ma_*.res animations
    /// bind by bone NAME.
    ///
    /// Leave null to keep Mario's body.
    /// </summary>
    [Export] public PackedScene BodyScene = null;

    /// <summary>
    /// Explicit mesh override. Only needed if you have extracted the mesh to its
    /// own .res; otherwise use BodyScene. Wins over BodyScene when both are set.
    /// Must be rigged to the SAME 29-bone SMS skeleton
    /// (mdl1 / center / jnt_waist / ...), since the shared ma_*.res animations
    /// bind by bone NAME. Leave null to keep Mario's mesh.
    /// </summary>
    [Export] public Mesh BodyMesh = null;

    /// <summary>
    /// Explicit Skin override, paired with BodyMesh. REQUIRED whenever BodyMesh
    /// is set explicitly — the two are a matched pair and mixing them across
    /// characters will deform the model badly. Not needed when using BodyScene.
    ///
    /// Note the two conventions in this project: Mario's mesh is normalised to
    /// ~2.01 units and its bind basis carries a 74.4414 scale, while Luigi's
    /// mesh is authored at skeleton scale (~122 units) with a 1.0 bind basis.
    /// Both are self-consistent against the same skeleton, so each mesh just
    /// needs to travel with its own Skin.
    /// </summary>
    [Export] public Skin BodySkin = null;

    /// <summary>
    /// Uniform scale for this character, applied to the Armature node (so the
    /// skeleton and anything bone-attached scale together). Deliberately does
    /// NOT touch the CollisionShape3D — physics is hand-tuned and stays shared.
    /// Use this to reconcile characters authored at different heights.
    /// 1.0 = leave as imported.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,3.0,0.001,or_greater")]
    public float BodyScale = 1.0f;

    // ------------------------------------------------------------ textures --

    /// <summary>
    /// Replacement for the main body texture atlas (*_ma_mdl1_0.png).
    /// Paint your custom colors on a copy of that texture and assign it here.
    /// Leave null to keep whatever the mesh already ships with.
    /// </summary>
    [Export] public Texture2D BodyTexture = null;

    /// <summary>
    /// Optional replacement for the eyes/iris texture.
    /// Leave null to keep the default.
    /// </summary>
    [Export] public Texture2D EyesTexture = null;

    /// <summary>
    /// Substring identifying which atlas is the BODY for this character, matched
    /// against each surface's existing albedo texture path.
    ///
    /// Characters do not agree on atlas numbering - Mario's eyes are on
    /// ma_mdl1_1 while Luigi's are on ma_mdl1_2 - so the mapping has to travel
    /// with the profile rather than being hardcoded.
    /// </summary>
    [Export] public string BodyTextureMatch = "ma_mdl1_0";

    /// <summary>
    /// Substring identifying which atlas holds the EYES for this character.
    /// Mario: "ma_mdl1_1". Luigi: "ma_mdl1_2".
    /// </summary>
    [Export] public string EyesTextureMatch = "ma_mdl1_1";

    // ----------------------------------------------------------- eye blinks --

    /// <summary>
    /// Open-eye texture, swapped onto the _mat_eye_L / _mat_eye_R surfaces at
    /// runtime. Leave null to use Mario's H_ma_eye1_s3tc.png.
    /// </summary>
    [Export] public Texture2D AwakeEyeTexture = null;

    /// <summary>
    /// Closed-eye texture used while sleeping. Leave null to use Mario's
    /// H_ma_eye1_s3tc_shut.png - which is just a skin-toned lid, so it reads
    /// fine on any character sharing Mario's palette.
    /// </summary>
    [Export] public Texture2D SleepingEyeTexture = null;

    // ---------------------------------------------------------------- hands --
    // The hands are separate rigged models hung off BoneAttachment3D nodes at
    // M_hand1_L / M_hand1_R, not part of the body mesh. Leave any of these null
    // to keep Mario's for that slot, so a character can override just the ones
    // that differ.

    [ExportGroup("Hands")]
    [Export] public PackedScene LeftHandClosed = null;
    [Export] public PackedScene LeftHandSlightlyOpen = null;
    [Export] public PackedScene RightHandClosed = null;
    [Export] public PackedScene RightHandSlightlyOpen = null;

    /// <summary>The held-cap variant. Mario's is a red M cap.</summary>
    [Export] public PackedScene RightHandWithHat = null;

    // ------------------------------------------------------------- headgear --

    /// <summary>
    /// Model worn on the head, attached to the <c>M_head_cap1</c> bone. Mario,
    /// Luigi and Koopa carry their headgear inside the body mesh, so they leave
    /// this null; Piantissimo's mask is a separate model (ma_cap1.bmd) and needs
    /// it. The BoneAttachment3D is created on demand, so the base scene doesn't
    /// have to carry an empty node for characters that never use one.
    /// </summary>
    [Export] public PackedScene HeadGear = null;

    /// <summary>
    /// Uniform scale for HeadGear, in case the part was ripped at a different
    /// scale from the body. 1.0 = as authored.
    /// </summary>
    [Export(PropertyHint.Range, "0.01,10.0,0.01,or_greater")]
    public float HeadGearScale = 1.0f;
}
