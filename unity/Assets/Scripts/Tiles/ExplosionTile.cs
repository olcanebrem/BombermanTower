using UnityEngine;

public class ExplosionTile : TileBase, IInitializable, ITurnBased
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public override TileType TileType => TileType.Explosion;

    [Header("Explosion Settings")]
    [SerializeField] private float explosionDuration = 1f;
    [SerializeField] private int explosionTurns = 2; // Kaç tur hasar versin

    // ITurnBased implementation
    public bool HasActedThisTurn { get; set; }
    
    private float timer;
    private int turnsActive = 0;

    void OnEnable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.Register(this);
    }
    
    public void Init(int x, int y)
    {
        X = x;
        Y = y;
        timer = 0f;
        turnsActive = 0;

        // Deal damage and handle tile destruction BEFORE registering ourselves
        var ll = LevelLoader.instance;
        if (ll != null && x >= 0 && x < ll.Width && y >= 0 && y < ll.Height)
        {
            // Get current tile information
            // Check layers for current objects
            var layeredGrid = LayeredGridService.Instance;
            bool hasDestructibleTile = layeredGrid != null && (layeredGrid.GetDestructibleTile(x, y) & LayeredGridService.LayerMask.Destructible) != 0;
            var objectsAtPosition = layeredGrid?.GetAllObjectsAt(x, y) ?? new System.Collections.Generic.List<GameObject>();
            GameObject existingTarget = objectsAtPosition.Count > 0 ? objectsAtPosition[0] : null;
            
            // Get current tile type from layered grid
            TileType currentTileType = layeredGrid?.GetTileTypeAt(x, y) ?? TileType.Empty;
            
            // Debug.Log($"[ExplosionTile] Analyzing position ({x},{y}) - current tile: {currentTileType}, object: {existingTarget?.name ?? "NULL"}");
            
            // Check if explosion should be created at this position
            if (!ExplosionPassableHelper.ShouldCreateExplosionTile(currentTileType, existingTarget))
            {
                Debug.Log($"[ExplosionTile] Should not create explosion at ({x},{y}) for tile type {currentTileType}");
                // Destroy this explosion tile as it shouldn't exist
                Destroy(gameObject);
                return;
            }
            
            bool targetWasDestroyed = false;
            
            // Handle existing objects at this position
            if (existingTarget != null && existingTarget != gameObject)
            {
                // Debug.Log($"[ExplosionTile] Found existing target at ({x},{y}): {existingTarget.name} (type: {currentTileType})");
                
                // Check if we can damage this target
                if (ExplosionPassableHelper.IsExplosionDamageable(currentTileType))
                {
                    if (existingTarget.TryGetComponent(out IDamageable dmg))
                    {
                        // Debug.Log($"[ExplosionTile] Damaging existing target {existingTarget.name} at ({x},{y})");
                        
                        // Check if target will be destroyed
                        bool willBeDestroyed = ExplosionPassableHelper.WillTileBeDestroyed(currentTileType, existingTarget);
                        
                        // Deal damage
                        dmg.TakeDamage(1);
                        
                        // If target was destroyed, mark the position
                        if (willBeDestroyed)
                        {
                            // Debug.Log($"[ExplosionTile] Target {existingTarget.name} at ({x},{y}) was destroyed by explosion");
                            targetWasDestroyed = true;
                            
                            // The target object should handle its own cleanup (RemoveEnemy, etc.)
                            // We just need to prepare the space for explosion
                        }
                    }
                    // else - Target doesn't have IDamageable but should
                }
                // else - Target is not damageable by explosions (normal)
            }
            // else - No existing target at position (normal)
            
            // Register this explosion in LevelLoader's tracking systems
            // ONLY update levelMap if no undamaged object remains at this position
            bool shouldRegisterInMap = true;
            
            if (existingTarget != null && existingTarget != gameObject)
            {
                // If there's an existing object that wasn't destroyed, don't overwrite levelMap
                if (!ExplosionPassableHelper.IsExplosionDamageable(currentTileType) || !targetWasDestroyed)
                {
                    shouldRegisterInMap = false;
                    // Debug.Log($"[ExplosionTile] Not registering explosion in levelMap - undamaged {currentTileType} remains at ({x},{y})");
                }
            }
            
            if (shouldRegisterInMap)
            {
                // Place explosion in effect layer
                if (layeredGrid != null)
                {
                    layeredGrid.PlaceEffect(gameObject, x, y);
                }
                // Debug.Log($"[ExplosionTile] Registered explosion in LevelLoader maps at ({x},{y})");
            }
            // else - Explosion exists visually but doesn't override logic map
        }

        // Debug.Log($"[ExplosionTile] Spawned at ({X},{Y}) - will be active for {explosionTurns} turns");
    }
    
    public void ResetTurn()
    {
        HasActedThisTurn = false;
    }
    
    public IGameAction GetAction()
    {
        if (HasActedThisTurn) return null;
        
        HasActedThisTurn = true;
        turnsActive++;
        
        // No need to deal damage each turn - damage is dealt once on creation
        // Explosion just exists visually for multiple turns
        // Debug.Log($"[ExplosionTile] Explosion at ({X},{Y}) active turn {turnsActive}/{explosionTurns}");
        
        // Check if should die after this turn
        if (turnsActive >= explosionTurns)
        {
            Debug.Log($"[ExplosionTile] Explosion at ({X},{Y}) finished after {turnsActive} turns");
            Die();
        }
        
        return null;
    }


    private void DealDamageAtPosition()
    {
        var ll = LevelLoader.instance;
        if (ll == null || X < 0 || X >= ll.Width || Y < 0 || Y >= ll.Height) 
        {
            Debug.LogWarning($"[ExplosionTile] Invalid position or LevelLoader: ({X},{Y})");
            return;
        }

        var layeredGrid = LayeredGridService.Instance;
        var objectsAtPosition = layeredGrid?.GetAllObjectsAt(X, Y) ?? new System.Collections.Generic.List<GameObject>();
        GameObject target = objectsAtPosition.Count > 0 ? objectsAtPosition[0] : null;
        if (target == null) 
        {
            Debug.Log($"[ExplosionTile] No target at ({X},{Y})");
            return;
        }

        Debug.Log($"[ExplosionTile] Found target at ({X},{Y}): {target.name}");

        if (target.TryGetComponent(out IDamageable dmg))
        {
            Debug.Log($"[ExplosionTile] Damaging {target.name} at ({X},{Y}) - target has IDamageable");
            dmg.TakeDamage(1);
        }
        else
        {
            Debug.Log($"[ExplosionTile] Target {target.name} at ({X},{Y}) does not have IDamageable component");
        }
    }

    private void Die()
    {
        Debug.Log($"[ExplosionTile] Explosion at ({X},{Y}) dying...");
        
        var ll = LevelLoader.instance;
        if (ll != null && X >= 0 && X < ll.Width && Y >= 0 && Y < ll.Height)
        {
            // Remove from effect layer
            var layeredGrid = LayeredGridService.Instance;
            if (layeredGrid != null && layeredGrid.IsValidPosition(X, Y))
            {
                layeredGrid.RemoveEffect(this.gameObject, X, Y);
            }
        }
        
        // Unregister from TurnManager
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }

        Destroy(gameObject);
    }
}