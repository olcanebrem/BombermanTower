using UnityEngine;

public class MoveAction : IGameAction
{
    private readonly IMovable mover;
    private readonly Vector2Int direction;
    private readonly GameObject actorGameObject; // Store the GameObject reference

    // IGameAction'ın yeni özelliği
    public GameObject Actor => actorGameObject; // Return the stored reference
    
    // Debug için direction property
    public Vector2Int Direction => direction;

    public MoveAction(IMovable mover, Vector2Int direction)
    {
        this.mover = mover;
        this.direction = direction;
        // Store the GameObject reference when the action is created
        this.actorGameObject = (mover as MonoBehaviour)?.gameObject;
    }

    public void Execute()
    {
        Debug.Log($"[MoveAction] Execute called - Direction: {direction}");
        
        // Check if the mover is still valid before trying to use it
        if (mover == null || (mover is MonoBehaviour mono && mono == null))
        {
            Debug.LogWarning("Trying to execute MoveAction with a destroyed mover");
            return;
        }

        Debug.Log($"[MoveAction] Mover valid: {mover.GetType().Name} at ({mover.X}, {mover.Y})");
        
        // Execute the movement if possible
        if (MovementHelper.TryMove(mover, direction, out Vector3 targetPos))
        {
            Debug.Log($"[MoveAction] TryMove SUCCESS - Target position: {targetPos}");
            mover.StartMoveAnimation(targetPos);
        }
        else
        {
            Debug.Log($"[MoveAction] TryMove FAILED - Direction: {direction}, Current position: ({mover.X}, {mover.Y})");
        }
    }
}