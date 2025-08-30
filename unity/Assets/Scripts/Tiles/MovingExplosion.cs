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
        
        // Check if tile is passable
        TileType tileType = TileSymbols.DataSymbolToType(ll.levelMap[nextX, nextY]);
        bool passable = true;
        try 
        {
            passable = MovementHelper.IsTilePassable(null, tileType);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MovingExplosion] IsTilePassable failed at ({nextX},{nextY}): {e.Message}");
            passable = false;
        }
        
        // Move to next position
        X = nextX;
        Y = nextY;
        transform.position = new Vector3(X * ll.tileSize, (ll.Height - Y - 1) * ll.tileSize, 0);
        
        // Create explosion at new position
        CreateExplosionTileAt(X, Y);
        
        remainingSteps--;
        Debug.Log($"[MovingExplosion] Moved to ({X},{Y}), {remainingSteps} steps remaining");
        
        // Stop if hit wall or obstacle
        if (!passable)
        {
            Debug.Log($"[MovingExplosion] Hit obstacle at ({X},{Y}), stopping");
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
        
        Debug.Log($"[MovingExplosion] Created ExplosionTile at ({x},{y})");
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