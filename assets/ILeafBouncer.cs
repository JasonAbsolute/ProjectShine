using Godot;

/// <summary>Anything a tree's leaf-collision body can report up to for a bounce
/// reaction when Mario lands on it — see Mario.cs's post-MoveAndSlide check.</summary>
public interface ILeafBouncer
{
    void Bounce(Vector3 marioPos);
}
