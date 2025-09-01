using UnityEngine;

public class AgentActionHandler
{
    private PlayerController playerController;
    private bool debugActions;

    private readonly Vector2Int[] moveDirections = new Vector2Int[]
    {
        Vector2Int.zero, new Vector2Int(0, -1), new Vector2Int(1, 0),
        new Vector2Int(0, 1), new Vector2Int(-1, 0),
        new Vector2Int(1, -1), new Vector2Int(1, 1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    public AgentActionHandler(PlayerController player, bool debug)
    {
        playerController = player;
        debugActions = debug;
    }

    public IGameAction CreateGameAction(int moveActionIndex, int bombActionIndex)
    {
        Debug.Log($"[AgentActionHandler] CreateGameAction - Move: {moveActionIndex}, Bomb: {bombActionIndex}");

        Vector2Int moveDirection = ConvertMoveAction(moveActionIndex);
        if (moveDirection != Vector2Int.zero)
        {
            if (debugActions) Debug.Log($"[AgentActionHandler] Creating MoveAction with direction: {moveDirection}");
            return new MoveAction(playerController, moveDirection);
        }

        if (bombActionIndex >= 1)
        {
            Vector2Int bombPlacement = FindBombPlacement();
            if (debugActions) Debug.Log($"[AgentActionHandler] Creating PlaceBombAction at direction: {bombPlacement}");
            return new PlaceBombAction(playerController, bombPlacement);
        }

        if (debugActions) Debug.Log("[AgentActionHandler] No movement or bomb action - creating MoveAction with zero vector");
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
        // Check adjacent tiles for empty space to place bomb
        Vector2Int[] directions = { Vector2Int.zero, Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        
        foreach (var dir in directions)
        {
            int targetX = playerController.X + dir.x;
            int targetY = playerController.Y + dir.y;
            
            var ll = LevelLoader.instance;
            if (ll != null && targetX >= 0 && targetX < ll.Width && targetY >= 0 && targetY < ll.Height)
            {
                if (LayeredGridService.Instance?.IsWalkable(targetX, targetY) ?? false)
                {
                    if (debugActions) Debug.Log($"[AgentActionHandler] Found empty space for bomb at direction: {dir}");
                    return dir;
                }
            }
        }
        
        if (debugActions) Debug.Log("[AgentActionHandler] No empty space found for bomb, using current position");
        return Vector2Int.zero; // Fallback to current position
    }
}