using UnityEngine;
using System;

public class BombTile : TileBase, ITurnBased, IInitializable, IDamageable
{
    [Header("Bomb Settings")]
    public int explosionRange = 4;
    public int turnsToExplode = 3;
    public GameObject movingExplosionPrefab;  // MovingExplosion creates ExplosionTiles automatically
    
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
        
        // Create center explosion - use MovingExplosion with 0 steps for consistency
        CreateMovingExplosionAt(X, Y, Vector2Int.zero, 0);
        
        // Die FIRST to clear this bomb from tileObjects
        Die();
        
        // Then create moving explosions in all directions  
        // Each direction starts one step away from bomb center
        CreateMovingExplosionAt(X + Vector2Int.up.x, Y + Vector2Int.up.y, Vector2Int.up, explosionRange - 1);
        CreateMovingExplosionAt(X + Vector2Int.down.x, Y + Vector2Int.down.y, Vector2Int.down, explosionRange - 1);
        CreateMovingExplosionAt(X + Vector2Int.left.x, Y + Vector2Int.left.y, Vector2Int.left, explosionRange - 1);
        CreateMovingExplosionAt(X + Vector2Int.right.x, Y + Vector2Int.right.y, Vector2Int.right, explosionRange - 1);
    }
    
    // Old explosion methods removed - now using MovingExplosion for all explosion logic
    
    private void CreateMovingExplosionAt(int startX, int startY, Vector2Int direction, int steps)
    {
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
            movingExplosion = movingExplosionGO.AddComponent<MovingExplosion>();
            // MovingExplosion prefab should have explosionTilePrefab already set
        }
        
        if (movingExplosion != null)
        {
            movingExplosion.InitMovingExplosion(startX, startY, direction, steps);
            // Debug.Log($"[BombTile] Successfully created MovingExplosion at ({startX},{startY}) moving {direction} for {steps} steps");
        }
        else
        {
            Debug.LogError($"[BombTile] Failed to get MovingExplosion component from instantiated prefab!");
        }
    }
    
    // CreateExplosionAt removed - MovingExplosion handles all explosion tile creation
    
    private void Die()
    {
        Debug.Log($"[BombTile] Bomb at ({X},{Y}) dying...");
        
        // Remove from layered system
        var layeredGrid = LayeredGridService.Instance;
        if (layeredGrid != null && layeredGrid.IsValidPosition(X, Y))
        {
            layeredGrid.RemoveBomb(this.gameObject, X, Y);
        }
        
        // Manual unregister to avoid OnDisable double call
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }
        
        Destroy(gameObject);
    }
}