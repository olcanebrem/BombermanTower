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
        targetWorldPos = Vector3.zero;
        
        // Safety checks
        if (mover == null || mover.gameObject == null || ll == null) 
        {
            Debug.LogError("[AtomicMovementHelper] FAILED - Mover or LevelLoader is null");
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
            targetWorldPos = ll.GridToWorld(new Vector2Int(targetX, targetY));
            
            // Handle interaction (collection, combat) at new position
            HandleInteractionAtPosition(mover, targetX, targetY);
            
            Debug.Log($"[AtomicMovementHelper] {mover.GetType().Name} successfully moved to ({targetX}, {targetY})");
            return true;
        }
        
        Debug.Log($"[AtomicMovementHelper] {mover.GetType().Name} movement blocked to ({targetX}, {targetY})");
        return false;
    }
    
    /// <summary>
    /// Handle interactions at the new position (item collection, combat)
    /// </summary>
    private static void HandleInteractionAtPosition(IMovable mover, int x, int y)
    {
        var ll = LevelLoader.instance;
        if (ll == null) return;
        
        // Check for collectibles
        TileType tileType = TileSymbols.DataSymbolToType(ll.levelMap[x, y]);
        GameObject objAtPosition = ll.tileObjects[x, y];
        
        // Item collection for players
        if (mover is PlayerController player && (tileType == TileType.Coin || tileType == TileType.Health))
        {
            var collectible = objAtPosition?.GetComponent<ICollectible>();
            if (collectible != null)
            {
                bool collected = collectible.OnCollect(player.gameObject);
                if (collected)
                {
                    ll.DestroyTileAt(x, y);
                    Debug.Log($"[AtomicMovementHelper] {player.GetType().Name} collected {tileType} at ({x}, {y})");
                }
            }
        }
        
        // Combat interactions
        bool moverIsPlayer = mover is PlayerController;
        bool targetIsEnemy = (tileType == TileType.Enemy || tileType == TileType.EnemyShooter);
        bool targetIsPlayer = (tileType == TileType.Player);
        
        if ((moverIsPlayer && targetIsEnemy) || (!moverIsPlayer && targetIsPlayer))
        {
            var damageable = objAtPosition?.GetComponent<IDamageable>();
            if (damageable != null && damageable.CurrentHealth > 0)
            {
                damageable.TakeDamage(1);
                Debug.Log($"[AtomicMovementHelper] {mover.GetType().Name} attacked {tileType} at ({x}, {y})");
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