using UnityEngine;

/// <summary>
/// Helper class for determining explosion behavior with different tile types
/// Handles passability rules and destruction logic specific to explosions
/// </summary>
public static class ExplosionPassableHelper
{
    /// <summary>
    /// Determines if explosion can pass through a tile type
    /// </summary>
    public static bool IsExplosionPassable(TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Empty:
            case TileType.Player:
            case TileType.Enemy:
            case TileType.EnemyShooter:
            case TileType.Coin:
            case TileType.Health:
            case TileType.Bomb:
            case TileType.Gate:
                return true; // Explosion can pass through these
                
            case TileType.Breakable:
                return false; // Explosion stops at breakables but damages them
                
            case TileType.Wall:
                return false; // Explosion cannot pass through walls
                
            case TileType.Explosion:
                return true; // Explosions can overlap
                
            default:
                return false; // Unknown tiles block explosions
        }
    }
    
    /// <summary>
    /// Determines if a tile type can be damaged by explosion
    /// </summary>
    public static bool IsExplosionDamageable(TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Player:
            case TileType.Enemy:
            case TileType.EnemyShooter:
            case TileType.Breakable:
            case TileType.Bomb:
                return true; // These can be damaged by explosions
                
            case TileType.Empty:
            case TileType.Wall:
            case TileType.Coin:
            case TileType.Health:
            case TileType.Gate:
            case TileType.Explosion:
                return false; // These are not affected by explosion damage
                
            default:
                return false; // Unknown tiles are not damageable
        }
    }
    
    /// <summary>
    /// Determines if explosion should continue after hitting this tile type
    /// </summary>
    public static bool ShouldExplosionContinue(TileType tileType, GameObject tileObject)
    {
        // First check base passability
        if (!IsExplosionPassable(tileType))
        {
            // If not passable, check if it can be damaged
            if (IsExplosionDamageable(tileType) && tileObject != null)
            {
                // Check if object has IDamageable component
                bool hasDamageableComponent = tileObject.TryGetComponent<IDamageable>(out _);
                if (hasDamageableComponent)
                {
                    // Explosion can damage this object but should stop here
                    return false;
                }
            }
            // Cannot pass and cannot damage - explosion stops
            return false;
        }
        
        // Explosion can pass through this tile
        return true;
    }
    
    /// <summary>
    /// Determines if explosion should create an ExplosionTile at this position
    /// </summary>
    public static bool ShouldCreateExplosionTile(TileType tileType, GameObject tileObject)
    {
        // Always create explosion tiles in empty spaces
        if (tileType == TileType.Empty)
        {
            return true;
        }
        
        // SPECIAL CASE: Don't create explosion tiles on collectibles
        // They should remain intact and collectible by players
        if (tileType == TileType.Coin || tileType == TileType.Health)
        {
            Debug.Log($"[ExplosionPassableHelper] Not creating explosion tile on collectible: {tileType}");
            return false;
        }
        
        // Create explosion if we can damage the object at this position
        if (IsExplosionDamageable(tileType) && tileObject != null)
        {
            return tileObject.TryGetComponent<IDamageable>(out _);
        }
        
        // Create explosion if explosion can pass through this tile
        if (IsExplosionPassable(tileType))
        {
            return true;
        }
        
        // Don't create explosion for walls and other impassable tiles
        return false;
    }
    
    /// <summary>
    /// Checks if a tile will be destroyed by explosion damage
    /// This is used to determine if the position should be marked as Empty after explosion
    /// </summary>
    public static bool WillTileBeDestroyed(TileType tileType, GameObject tileObject)
    {
        // Only certain tile types get destroyed by explosions
        switch (tileType)
        {
            case TileType.Breakable:
            case TileType.Bomb:
                return true; // These are always destroyed by explosions
                
            case TileType.Enemy:
            case TileType.EnemyShooter:
                // Enemies might be destroyed if they have low health
                if (tileObject != null && tileObject.TryGetComponent<IDamageable>(out var damageable))
                {
                    // Assume 1 damage will destroy enemies with 1 health
                    return damageable.CurrentHealth <= 1;
                }
                return false;
                
            case TileType.Player:
                // Players don't get destroyed, just take damage
                return false;
                
            default:
                return false; // Other tiles don't get destroyed
        }
    }
}