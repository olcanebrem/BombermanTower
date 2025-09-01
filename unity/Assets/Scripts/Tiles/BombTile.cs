using UnityEngine;
using System;

public class BombTile : TileBase, ITurnBased, IInitializable, IDamageable
{
    [Header("Bomb Settings")]
    public int explosionRange = 4;
    public int turnsToExplode = 3;
    public GameObject explosionPrefab;
    public GameObject movingExplosionPrefab;
    
    public int X { get; set; }
    public int Y { get; set; }
    public override TileType TileType => TileType.Bomb;
    public bool HasActedThisTurn { get; set; }
    
    // Owner property for layered system - who placed this bomb
    public GameObject Owner { get; set; }
    
    // IDamageable implementation
    public int CurrentHealth { get; private set; } = 1;
    public int MaxHealth => 1;
    public event Action OnHealthChanged;
    
    private int turnCounter = 0;
    private bool hasExploded = false;  // Çifte patlama koruması
    
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
    
    public void ResetTurn()
    {
        HasActedThisTurn = false;
    }
    
    public void TakeDamage(int damageAmount)
    {
        if (hasExploded || CurrentHealth <= 0) return;
        
        CurrentHealth = 0;
        OnHealthChanged?.Invoke();
        Debug.Log($"[BombTile] Bomb at ({X},{Y}) triggered by damage!");
        
        Explode();
    }
    
    public IGameAction GetAction()
    {
        if (HasActedThisTurn || hasExploded) return null;
        
        HasActedThisTurn = true;
        turnCounter++;
        
        if (turnCounter >= turnsToExplode)
        {
            Debug.Log($"[BombTile] Bomb at ({X},{Y}) timer expired!");
            Explode();
        }
        
        return null;
    }
    
    private void Explode()
    {
        if (hasExploded) return;  // Çifte patlama koruması
        hasExploded = true;
        
        CurrentHealth = 0;
        Debug.Log($"[BombTile] Bomb at ({X},{Y}) exploding with range {explosionRange}!");
        
        // Create center explosion immediately
        CreateExplosionAt(X, Y);
        
        // Die FIRST to clear this bomb from tileObjects
        Die();
        
        // Then create moving explosions in all directions
        // Moving explosions will create explosions as they move, but not at their starting positions
        CreateMovingExplosionInDirection(Vector2Int.up);
        CreateMovingExplosionInDirection(Vector2Int.down);
        CreateMovingExplosionInDirection(Vector2Int.left);
        CreateMovingExplosionInDirection(Vector2Int.right);
    }
    
    private void CreateExplosionInDirection(Vector2Int direction)
    {
        var ll = LevelLoader.instance;
        if (ll == null) return;
        
        for (int i = 1; i <= explosionRange; i++)
        {
            int targetX = X + (direction.x * i);
            int targetY = Y + (direction.y * i);
            
            // Bounds check
            if (targetX < 0 || targetX >= ll.Width || targetY < 0 || targetY >= ll.Height)
                break;
            
            // Create explosion at this position first
            CreateExplosionAt(targetX, targetY);
            
            // Then check if we should continue (passable check)
            TileType tileType = TileSymbols.DataSymbolToType(ll.levelMap[targetX, targetY]);
            
            // Safe passable check - just use tileType directly
            bool passable = true;
            try 
            {
                passable = MovementHelper.IsTilePassable(null, tileType);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BombTile] IsTilePassable failed at ({targetX},{targetY}): {e.Message}");
                passable = false;
            }
            
            if (!passable)
                break; // Hit wall/obstacle, stop explosion
        }
    }
    
    private void CreateMovingExplosionInDirection(Vector2Int direction)
    {
        // Debug.Log($"[BombTile] CreateMovingExplosionInDirection called for direction {direction}");
        
        var ll = LevelLoader.instance;
        if (ll == null) 
        {
            Debug.LogError("[BombTile] LevelLoader.instance is null!");
            return;
        }
        
        if (movingExplosionPrefab == null) 
        {
            Debug.LogError("[BombTile] movingExplosionPrefab is null! Please assign it in the Inspector.");
            return;
        }
        
        // Debug.Log($"[BombTile] LevelLoader and prefab OK, creating moving explosion...");
        
        // Starting position is one step in the direction from bomb
        int startX = X + direction.x;
        int startY = Y + direction.y;
        
        // Check bounds for starting position
        if (startX < 0 || startX >= ll.Width || startY < 0 || startY >= ll.Height)
        {
            // Debug.Log($"[BombTile] MovingExplosion start position out of bounds: ({startX},{startY})");
            return;
        }
        
        Vector3 pos = new Vector3(startX * ll.tileSize, (ll.Height - startY - 1) * ll.tileSize, 0);
        Transform effectsParent = ll.dynamicParent ?? ll.levelContentParent;
        
        GameObject movingExplosionGO = Instantiate(movingExplosionPrefab, pos, Quaternion.identity, effectsParent);
        MovingExplosion movingExplosion = movingExplosionGO.GetComponent<MovingExplosion>();
        
        if (movingExplosion == null)
        {
            // Debug.LogWarning($"[BombTile] MovingExplosion component not found, adding it to {movingExplosionGO.name}");
            movingExplosion = movingExplosionGO.AddComponent<MovingExplosion>();
            movingExplosion.explosionTilePrefab = explosionPrefab; // Give it reference to explosion tile prefab
        }
        
        if (movingExplosion != null)
        {
            // Initialize with remaining steps (explosionRange - 1 because we already moved one step)
            movingExplosion.InitMovingExplosion(startX, startY, direction, explosionRange - 1);
            // Debug.Log($"[BombTile] Successfully created MovingExplosion at ({startX},{startY}) moving {direction} for {explosionRange - 1} steps");
        }
        else
        {
            Debug.LogError($"[BombTile] Failed to get MovingExplosion component from instantiated prefab!");
        }
    }
    
    private void CreateExplosionAt(int x, int y)
    {
        var ll = LevelLoader.instance;
        if (ll == null) 
        {
            Debug.LogError("[BombTile] LevelLoader.instance is null!");
            return;
        }
        
        if (explosionPrefab == null) 
        {
            Debug.LogError("[BombTile] explosionPrefab is null!");
            return;
        }
        
        // Validate position bounds
        if (x < 0 || x >= ll.Width || y < 0 || y >= ll.Height)
        {
            Debug.Log($"[BombTile] Position ({x},{y}) out of bounds, skipping explosion creation");
            return;
        }
        
        // Check what's currently at this position
        TileType currentTileType = TileSymbols.DataSymbolToType(ll.levelMap[x, y]);
        GameObject currentObject = ll.tileObjects[x, y];
        
        // For bomb center explosion, always create (bomb position should be valid)
        // For other positions, check passability and IDamageable
        if (x != X || y != Y) // Not the bomb center
        {
            // Check if tile is passable
            bool isPassable = true;
            try 
            {
                isPassable = MovementHelper.IsTilePassable(null, currentTileType);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BombTile] IsTilePassable check failed at ({x},{y}): {e.Message}");
                isPassable = false;
            }
            
            // If tile is not passable and has no IDamageable component, skip explosion
            if (!isPassable)
            {
                bool hasDamageableComponent = false;
                if (currentObject != null)
                {
                    hasDamageableComponent = currentObject.TryGetComponent<IDamageable>(out _);
                }
                
                if (!hasDamageableComponent)
                {
                    Debug.Log($"[BombTile] Tile at ({x},{y}) is not passable and has no IDamageable component (type: {currentTileType}), skipping explosion creation");
                    return;
                }
                else
                {
                    Debug.Log($"[BombTile] Tile at ({x},{y}) is not passable but has IDamageable component (type: {currentTileType}), creating explosion to damage it");
                }
            }
        }
        
        // Debug.Log($"[BombTile] Creating explosion at ({x},{y}) - current tile type: {currentTileType}");
        
        Vector3 pos = new Vector3(x * ll.tileSize, (ll.Height - y - 1) * ll.tileSize, 0);
        Transform effectsParent = ll.dynamicParent ?? ll.levelContentParent;
        
        // Debug.Log($"[BombTile] Instantiating explosion prefab: {explosionPrefab.name} at position: {pos} under parent: {effectsParent?.name}");
        
        GameObject explosionGO = Instantiate(explosionPrefab, pos, Quaternion.identity, effectsParent);
        ExplosionTile explosion = explosionGO.GetComponent<ExplosionTile>();
        
        if (explosion == null)
        {
            // Debug.LogWarning($"[BombTile] ExplosionTile component not found, adding it to {explosionGO.name}");
            
            // Remove old ExplosionWave if exists
            var oldExplosion = explosionGO.GetComponent<ExplosionWave>();
            if (oldExplosion != null)
            {
                // Debug.Log($"[BombTile] Removing old ExplosionWave component");
                DestroyImmediate(oldExplosion);
            }
            
            // Add new ExplosionTile
            explosion = explosionGO.AddComponent<ExplosionTile>();
        }
        
        if (explosion != null)
        {
            // Debug.Log($"[BombTile] ExplosionTile component ready, calling Init({x},{y})");
            explosion.Init(x, y);
        }
        
        // Debug.Log($"[BombTile] Explosion creation completed at ({x},{y})");
    }
    
    private void Die()
    {
        Debug.Log($"[BombTile] Bomb at ({X},{Y}) dying...");
        
        var ll = LevelLoader.instance;
        if (ll != null && X >= 0 && X < ll.Width && Y >= 0 && Y < ll.Height)
        {
            ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            ll.tileObjects[X, Y] = null;
        }
        
        // Manual unregister to avoid OnDisable double call
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }
        
        Destroy(gameObject);
    }
}