using UnityEngine;
using System;

public class BombTile : TileBase, ITurnBased, IInitializable, IDamageable
{
    [Header("Bomb Settings")]
    public int explosionRange = 4;
    public int turnsToExplode = 3;
    public GameObject explosionPrefab;
    
    public int X { get; set; }
    public int Y { get; set; }
    public override TileType TileType => TileType.Bomb;
    public bool HasActedThisTurn { get; set; }
    
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
        
        // Die FIRST to clear this bomb from tileObjects
        Die();
        
        // Then create explosions in all directions
        CreateExplosionInDirection(Vector2Int.up);
        CreateExplosionInDirection(Vector2Int.down);
        CreateExplosionInDirection(Vector2Int.left);
        CreateExplosionInDirection(Vector2Int.right);
        
        // Center explosion to damage anything that might be at the bomb's position
        CreateExplosionAt(X, Y);
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
        
        Debug.Log($"[BombTile] Creating explosion at ({x},{y})");
        
        Vector3 pos = new Vector3(x * ll.tileSize, (ll.Height - y - 1) * ll.tileSize, 0);
        Transform effectsParent = ll.dynamicParent ?? ll.levelContentParent;
        
        Debug.Log($"[BombTile] Instantiating explosion prefab: {explosionPrefab.name} at position: {pos} under parent: {effectsParent?.name}");
        
        GameObject explosionGO = Instantiate(explosionPrefab, pos, Quaternion.identity, effectsParent);
        ExplosionTile explosion = explosionGO.GetComponent<ExplosionTile>();
        
        if (explosion == null)
        {
            Debug.LogWarning($"[BombTile] ExplosionTile component not found, adding it to {explosionGO.name}");
            
            // Remove old ExplosionWave if exists
            var oldExplosion = explosionGO.GetComponent<ExplosionWave>();
            if (oldExplosion != null)
            {
                Debug.Log($"[BombTile] Removing old ExplosionWave component");
                DestroyImmediate(oldExplosion);
            }
            
            // Add new ExplosionTile
            explosion = explosionGO.AddComponent<ExplosionTile>();
        }
        
        if (explosion != null)
        {
            Debug.Log($"[BombTile] ExplosionTile component ready, calling Init({x},{y})");
            explosion.Init(x, y);
        }
        
        Debug.Log($"[BombTile] Explosion creation completed at ({x},{y})");
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