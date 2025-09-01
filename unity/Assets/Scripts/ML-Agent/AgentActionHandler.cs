using UnityEngine;
using Debug = UnityEngine.Debug;

public class AgentActionHandler
{
    private readonly PlayerController playerController;
    private readonly bool debugActions;

    private readonly Vector2Int[] moveDirections = 
    {
        Vector2Int.zero,        // 0: No movement
        new Vector2Int(0, -1),  // 1: Up
        new Vector2Int(1, 0),   // 2: Right
        new Vector2Int(0, 1),   // 3: Down
        new Vector2Int(-1, 0),  // 4: Left
        new Vector2Int(1, -1),  // 5: Up-Right
        new Vector2Int(1, 1),   // 6: Down-Right
        new Vector2Int(-1, 1),  // 7: Down-Left
        new Vector2Int(-1, -1)  // 8: Up-Left
    };

    public AgentActionHandler(PlayerController player, bool debug)
    {
        playerController = player;
        debugActions = debug;
    }

    public IGameAction CreateGameAction(int moveActionIndex, int bombActionIndex)
    {
        Vector2Int moveDirection = ConvertMoveAction(moveActionIndex);

        if (moveDirection != Vector2Int.zero)
        {
            if (debugActions) Debug.Log($"[AgentActionHandler] Creating MoveAction with direction: {moveDirection}");
            return new MoveAction(playerController, moveDirection);
        }

        if (bombActionIndex >= 1)
        {
            if (debugActions) Debug.Log($"[AgentActionHandler] Creating PlaceBombAction");
            return new PlaceBombAction(playerController, FindBombPlacement());
        }

        if (debugActions) Debug.Log("[AgentActionHandler] No specific action, creating MoveAction with zero vector (Wait Action).");
        return new MoveAction(playerController, Vector2Int.zero);
    }

    private Vector2Int ConvertMoveAction(int actionIndex)
    {
        if (actionIndex >= 0 && actionIndex < moveDirections.Length)
        {
            return moveDirections[actionIndex];
        }
        return Vector2Int.zero;
    }

    private Vector2Int FindBombPlacement()
    {
        Vector2Int[] directions = { Vector2Int.zero, Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in directions)
        {
            int targetX = playerController.X + dir.x;
            int targetY = playerController.Y + dir.y;

            if (LayeredGridService.Instance?.IsValidPosition(targetX, targetY) ?? false)
            {
                if (LayeredGridService.Instance?.IsWalkable(targetX, targetY) ?? false)
                {
                    if (debugActions) Debug.Log($"[AgentActionHandler] Found empty space for bomb at direction: {dir}");
                    return dir;
                }
            }
        }
        return Vector2Int.zero;
    }
}