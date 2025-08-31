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
        // Debug.Log($"[MoveAction] Execute called - Direction: {direction}");
        
        // Check if the mover is still valid before trying to use it
        if (mover == null)
        {
            Debug.LogWarning("Trying to execute MoveAction with a null mover");
            return;
        }
        
        // Check if the mover's GameObject is destroyed (Unity-specific)
        MonoBehaviour moverMono = mover as MonoBehaviour;
        if (moverMono == null || moverMono.gameObject == null)
        {
            Debug.LogWarning("Trying to execute MoveAction with a destroyed mover GameObject");
            return;
        }

        // Debug.Log($"[MoveAction] Mover valid: {mover.GetType().Name} at ({mover.X}, {mover.Y})");
        
        // Execute the movement if possible
        try
        {
            // Debug.Log($"[MoveAction] Calling MovementHelper.TryMove with direction: {direction}");
            bool moveSuccessful = MovementHelper.TryMove(mover, direction, out Vector3 targetPos);
            // Debug.Log($"[MoveAction] MovementHelper.TryMove returned: {moveSuccessful}, targetPos: {targetPos}");
            
            if (moveSuccessful)
            {
                // Debug.Log($"[MoveAction] Starting animation to: {targetPos}");
                mover.StartMoveAnimation(targetPos);
            }
            // else - Movement failed, no animation
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MoveAction] MovementHelper.TryMove failed: {e.Message}");
        }
    }
}