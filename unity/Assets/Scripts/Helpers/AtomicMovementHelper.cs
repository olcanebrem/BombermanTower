using UnityEngine;

/// <summary>
/// Simplified atomic movement helper to prevent race conditions
/// Uses LevelLoader's atomic operations for conflict-free movement
/// </summary>
public static class AtomicMovementHelper
{
    /// <summary>
    /// Atomic movement operation using LevelLoader's TryMoveObject
    /// Returns true if movement successful, false if destination occupied
    /// </summary>
    public static bool TryMove(IMovable mover, Vector2Int direction, out Vector3 targetWorldPos)
    {
        var ll = LevelLoader.instance;
        var layeredGrid = LayeredGridService.Instance;
        targetWorldPos = Vector3.zero;
        
        // Safety checks
        if (mover == null || mover.gameObject == null || ll == null || layeredGrid == null) 
        {
            Debug.LogError("[AtomicMovementHelper] FAILED - Required components are null");
            return false;
        }
        
        // Calculate target position
        int targetX = mover.X + direction.x;
        int targetY = mover.Y + direction.y;
        
        // Use atomic movement operation
        bool moveSuccessful = ll.TryMoveObject(mover.gameObject, mover.X, mover.Y, targetX, targetY);
        
        if (moveSuccessful)
        {
            // Update mover's position tracking
            mover.OnMoved(targetX, targetY);
            
            // Calculate world position for animation
            targetWorldPos = layeredGrid.GridToWorld(targetX, targetY);
            
            // Handle interaction (collection, combat) at new position
            HandleInteractionAtPosition(mover, targetX, targetY, layeredGrid);
            
            Debug.Log($"[AtomicMovementHelper] {mover.GetType().Name} successfully moved to ({targetX}, {targetY})");
            return true;
        }
        
        Debug.Log($"[AtomicMovementHelper] {mover.GetType().Name} movement blocked to ({targetX}, {targetY})");
        return false;
    }
    
    /// <summary>
    /// Handle interactions at the new position (item collection, combat)
    /// </summary>
    private static void HandleInteractionAtPosition(IMovable mover, int x, int y, LayeredGridService layeredGrid)
    {
        // Check for items at new position
        GameObject itemAtPosition = layeredGrid.GetItemAt(x, y);
        if (itemAtPosition != null && mover is PlayerController player)
        {
            var collectible = itemAtPosition.GetComponent<ICollectible>();
            if (collectible != null)
            {
                bool collected = collectible.OnCollect(player.gameObject);
                if (collected)
                {
                    layeredGrid.RemoveItem(itemAtPosition, x, y);
                    Object.Destroy(itemAtPosition);
                    Debug.Log($"[AtomicMovementHelper] {player.GetType().Name} collected item at ({x}, {y})");
                }
            }
        }
        
        // Check for actors at new position for combat
        GameObject actorAtPosition = layeredGrid.GetActorAt(x, y);
        if (actorAtPosition != null && actorAtPosition != mover.gameObject)
        {
            bool moverIsPlayer = mover is PlayerController;
            
            var targetTile = actorAtPosition.GetComponent<TileBase>();
            if (targetTile != null)
            {
                bool targetIsEnemy = (targetTile.TileType == TileType.Enemy || targetTile.TileType == TileType.EnemyShooter);
                bool targetIsPlayer = (targetTile.TileType == TileType.Player);
                
                // Combat: Player vs Enemy or Enemy vs Player
                if ((moverIsPlayer && targetIsEnemy) || (!moverIsPlayer && targetIsPlayer))
                {
                    var damageable = actorAtPosition.GetComponent<IDamageable>();
                    if (damageable != null && damageable.CurrentHealth > 0)
                    {
                        damageable.TakeDamage(1);
                        Debug.Log($"[AtomicMovementHelper] {mover.GetType().Name} attacked {targetTile.TileType} at ({x}, {y})");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Check if the tile type represents a unit (can be attacked/interacted with)
    /// </summary>
    private static bool IsUnit(TileType type)
    {
        return type == TileType.Player || type == TileType.Enemy || type == TileType.EnemyShooter;
    }
}