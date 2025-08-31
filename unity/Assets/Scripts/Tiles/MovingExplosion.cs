using UnityEngine;

/// <summary>
/// Moving explosion that travels step by step in a direction, creating ExplosionTiles as it moves
/// </summary>
public class MovingExplosion : TileBase, ITurnBased, IInitializable, IMovable
{
    public int X { get; set; }
    public int Y { get; set; }
    public override TileType TileType => TileType.Explosion;
    public bool HasActedThisTurn { get; set; }
    
    [Header("Moving Explosion Settings")]
    public GameObject explosionTilePrefab;
    
    private Vector2Int direction;
    private int remainingSteps;
    private Vector2Int lastFacingDirection;
    
    void OnEnable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.Register(this);
    }
    
    public void Init(int x, int y)
    {
        X = x;
        Y = y;
    }
    
    /// <summary>
    /// Initialize the moving explosion with direction and range
    /// </summary>
    public void InitMovingExplosion(int startX, int startY, Vector2Int moveDirection, int steps)
    {
        X = startX;
        Y = startY;
        direction = moveDirection;
        remainingSteps = steps;
        lastFacingDirection = moveDirection;
        
        Debug.Log($"[MovingExplosion] Started at ({X},{Y}) moving {direction} for {remainingSteps} steps");
        
        // Create explosion at starting position immediately
        // This covers the first tile adjacent to the bomb
        CreateExplosionTileAt(X, Y);
    }
    
    public void ResetTurn()
    {
        HasActedThisTurn = false;
    }
    
    public IGameAction GetAction()
    {
        if (HasActedThisTurn || remainingSteps <= 0) return null;
        
        HasActedThisTurn = true;
        
        // Calculate next position
        int nextX = X + direction.x;
        int nextY = Y + direction.y;
        
        var ll = LevelLoader.instance;
        if (ll == null)
        {
            Die();
            return null;
        }
        
        // Check bounds
        if (nextX < 0 || nextX >= ll.Width || nextY < 0 || nextY >= ll.Height)
        {
            Debug.Log($"[MovingExplosion] Hit boundary at ({nextX},{nextY}), stopping");
            Die();
            return null;
        }
        
        // Check what's at the next position and validate if explosion can be created there
        TileType nextTileType = TileSymbols.DataSymbolToType(ll.levelMap[nextX, nextY]);
        GameObject nextObject = ll.tileObjects[nextX, nextY];
        
        // Check if tile is passable
        bool isPassable = true;
        try 
        {
            isPassable = MovementHelper.IsTilePassable(null, nextTileType);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MovingExplosion] IsTilePassable failed at ({nextX},{nextY}): {e.Message}");
            isPassable = false;
        }
        
        // If tile is not passable, check if it has IDamageable component
        bool canCreateExplosion = isPassable; // If passable, explosion can be created
        if (!isPassable)
        {
            // Check for IDamageable component
            bool hasDamageableComponent = false;
            if (nextObject != null)
            {
                hasDamageableComponent = nextObject.TryGetComponent<IDamageable>(out _);
            }
            
            if (hasDamageableComponent)
            {
                canCreateExplosion = true; // Can damage the object, so create explosion
                Debug.Log($"[MovingExplosion] Next position ({nextX},{nextY}) is not passable but has IDamageable, will create explosion and stop");
            }
            else
            {
                canCreateExplosion = false; // Cannot create explosion here
                Debug.Log($"[MovingExplosion] Next position ({nextX},{nextY}) is not passable and has no IDamageable (type: {nextTileType}), stopping without moving");
            }
        }
        
        // If we cannot create explosion at next position, stop here
        if (!canCreateExplosion)
        {
            Debug.Log($"[MovingExplosion] Cannot create explosion at ({nextX},{nextY}), stopping at current position ({X},{Y})");
            Die();
            return null;
        }
        
        // Move to next position
        X = nextX;
        Y = nextY;
        transform.position = new Vector3(X * ll.tileSize, (ll.Height - Y - 1) * ll.tileSize, 0);
        
        // Create explosion at new position (we already validated this is ok)
        CreateExplosionTileAt(X, Y);
        
        remainingSteps--;
        Debug.Log($"[MovingExplosion] Moved to ({X},{Y}), {remainingSteps} steps remaining");
        
        // If we hit a non-passable tile (even with IDamageable), stop here
        if (!isPassable)
        {
            Debug.Log($"[MovingExplosion] Hit non-passable tile at ({X},{Y}), stopping after creating explosion");
            Die();
            return null;
        }
        
        // Check if finished
        if (remainingSteps <= 0)
        {
            Debug.Log($"[MovingExplosion] Finished at ({X},{Y})");
            Die();
        }
        
        return null;
    }
    
    private void CreateExplosionTileAt(int x, int y)
    {
        var ll = LevelLoader.instance;
        if (ll == null || explosionTilePrefab == null) return;
        
        // Validate position bounds
        if (x < 0 || x >= ll.Width || y < 0 || y >= ll.Height)
        {
            Debug.Log($"[MovingExplosion] Position ({x},{y}) out of bounds, skipping explosion creation");
            return;
        }
        
        // Check what's currently at this position
        TileType currentTileType = TileSymbols.DataSymbolToType(ll.levelMap[x, y]);
        GameObject currentObject = ll.tileObjects[x, y];
        
        // Check if tile is passable - if not, don't create explosion there
        bool isPassable = true;
        try 
        {
            isPassable = MovementHelper.IsTilePassable(null, currentTileType);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MovingExplosion] IsTilePassable check failed at ({x},{y}): {e.Message}");
            isPassable = false;
        }
        
        // If tile is not passable (wall, etc.) and has no IDamageable component, skip explosion
        if (!isPassable)
        {
            bool hasDamageableComponent = false;
            if (currentObject != null)
            {
                hasDamageableComponent = currentObject.TryGetComponent<IDamageable>(out _);
            }
            
            if (!hasDamageableComponent)
            {
                Debug.Log($"[MovingExplosion] Tile at ({x},{y}) is not passable and has no IDamageable component (type: {currentTileType}), skipping explosion creation");
                return;
            }
            else
            {
                Debug.Log($"[MovingExplosion] Tile at ({x},{y}) is not passable but has IDamageable component (type: {currentTileType}), creating explosion to damage it");
            }
        }
        
        Vector3 pos = new Vector3(x * ll.tileSize, (ll.Height - y - 1) * ll.tileSize, 0);
        Transform effectsParent = ll.dynamicParent ?? ll.levelContentParent;
        
        GameObject explosionGO = Instantiate(explosionTilePrefab, pos, Quaternion.identity, effectsParent);
        ExplosionTile explosion = explosionGO.GetComponent<ExplosionTile>();
        
        if (explosion == null)
        {
            // Remove old ExplosionWave if exists and add ExplosionTile
            var oldExplosion = explosionGO.GetComponent<ExplosionWave>();
            if (oldExplosion != null)
            {
                DestroyImmediate(oldExplosion);
            }
            explosion = explosionGO.AddComponent<ExplosionTile>();
        }
        
        if (explosion != null)
        {
            explosion.Init(x, y);
        }
        
        Debug.Log($"[MovingExplosion] Created ExplosionTile at ({x},{y}) - current tile type: {currentTileType}");
    }
    
    // IMovable interface methods (required but not used for this implementation)
    public void OnMoved(int newX, int newY)
    {
        X = newX;
        Y = newY;
    }
    
    public void StartMoveAnimation(Vector3 targetPosition)
    {
        // Animation handled by transform position update
    }
    
    private void Die()
    {
        Debug.Log($"[MovingExplosion] MovingExplosion at ({X},{Y}) dying...");
        
        // Unregister from TurnManager
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }
        
        Destroy(gameObject);
    }
}