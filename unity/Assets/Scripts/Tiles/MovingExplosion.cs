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
        
        // Debug.Log($"[MovingExplosion] Started at ({X},{Y}) moving {direction} for {remainingSteps} steps");
        
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
            // Debug.Log($"[MovingExplosion] Hit boundary at ({nextX},{nextY}), stopping");
            Die();
            return null;
        }
        
        // Check what's at the next position using layered system
        var layeredGrid = LayeredGridService.Instance;
        bool canPass = layeredGrid?.IsFirePassable(nextX, nextY) ?? false;
        var objectsAtPosition = layeredGrid?.GetAllObjectsAt(nextX, nextY) ?? new System.Collections.Generic.List<GameObject>();
        
        // Get tile type at next position
        TileType nextTileType = layeredGrid?.GetTileTypeAt(nextX, nextY) ?? TileType.Empty;
        GameObject nextObject = objectsAtPosition.Count > 0 ? objectsAtPosition[0] : null;
        
        // Use ExplosionPassableHelper to determine explosion behavior
        bool canCreateExplosion = ExplosionPassableHelper.ShouldCreateExplosionTile(nextTileType, nextObject);
        bool shouldContinue = ExplosionPassableHelper.ShouldExplosionContinue(nextTileType, nextObject);
        
        // Debug.Log($"[MovingExplosion] Next position ({nextX},{nextY}) analysis - Type: {nextTileType}, CanCreate: {canCreateExplosion}, ShouldContinue: {shouldContinue}");
        
        // If we cannot create explosion at next position, stop here
        if (!canCreateExplosion)
        {
            // Debug.Log($"[MovingExplosion] Cannot create explosion at ({nextX},{nextY}), stopping at current position ({X},{Y})");
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
        // Debug.Log($"[MovingExplosion] Moved to ({X},{Y}), {remainingSteps} steps remaining");
        
        // If we should not continue after this tile, stop here
        if (!shouldContinue)
        {
            // Debug.Log($"[MovingExplosion] Should not continue after tile at ({X},{Y}), stopping after creating explosion");
            Die();
            return null;
        }
        
        // Check if finished
        if (remainingSteps <= 0)
        {
            // Debug.Log($"[MovingExplosion] Finished at ({X},{Y})");
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
        
        // Check what's currently at this position using layered system
        var layeredGrid = LayeredGridService.Instance;
        var objectsAtPosition = layeredGrid?.GetAllObjectsAt(x, y) ?? new System.Collections.Generic.List<GameObject>();
        GameObject currentObject = objectsAtPosition.Count > 0 ? objectsAtPosition[0] : null;
        
        // Get current tile type from layered grid
        TileType currentTileType = layeredGrid?.GetTileTypeAt(x, y) ?? TileType.Empty;
        
        // Use ExplosionPassableHelper to check if explosion should be created here
        if (!ExplosionPassableHelper.ShouldCreateExplosionTile(currentTileType, currentObject))
        {
            // Debug.Log($"[MovingExplosion] Should not create explosion at ({x},{y}) for tile type {currentTileType}");
            return;
        }
        
        Vector3 pos = new Vector3(x * ll.tileSize, (ll.Height - y - 1) * ll.tileSize, 0);
        Transform effectsParent = ll.dynamicParent ?? ll.levelContentParent;
        
        GameObject explosionGO = Instantiate(explosionTilePrefab, pos, Quaternion.identity, effectsParent);
        ExplosionTile explosion = explosionGO.GetComponent<ExplosionTile>();
        
        if (explosion == null)
        {
            // Add ExplosionTile component if needed
            explosion = explosionGO.AddComponent<ExplosionTile>();
        }
        
        if (explosion != null)
        {
            explosion.Init(x, y);
        }
        
        // Debug.Log($"[MovingExplosion] Created ExplosionTile at ({x},{y}) - current tile type: {currentTileType}");
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