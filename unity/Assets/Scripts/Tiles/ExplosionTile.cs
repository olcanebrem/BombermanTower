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

        // Deal damage to whatever was at this position BEFORE registering ourselves
        var ll = LevelLoader.instance;
        if (ll != null && x >= 0 && x < ll.Width && y >= 0 && y < ll.Height)
        {
            // Check for existing objects at this position and damage them
            GameObject existingTarget = ll.tileObjects[x, y];
            if (existingTarget != null && existingTarget != gameObject)
            {
                Debug.Log($"[ExplosionTile] Found existing target at ({x},{y}): {existingTarget.name}");
                
                if (existingTarget.TryGetComponent(out IDamageable dmg))
                {
                    Debug.Log($"[ExplosionTile] Damaging existing target {existingTarget.name} at ({x},{y})");
                    dmg.TakeDamage(1);
                }
                else
                {
                    Debug.Log($"[ExplosionTile] Existing target {existingTarget.name} at ({x},{y}) does not have IDamageable component");
                }
            }
            else
            {
                Debug.Log($"[ExplosionTile] No existing target at ({x},{y}) to damage");
            }
            
            // Now register this explosion in LevelLoader's tracking systems
            ll.levelMap[x, y] = TileSymbols.TypeToDataSymbol(TileType.Explosion);
            ll.tileObjects[x, y] = gameObject;
            
            Debug.Log($"[ExplosionTile] Registered explosion in LevelLoader maps at ({x},{y})");
        }

        Debug.Log($"[ExplosionTile] Spawned at ({X},{Y}) - will be active for {explosionTurns} turns");
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
        Debug.Log($"[ExplosionTile] Explosion at ({X},{Y}) active turn {turnsActive}/{explosionTurns}");
        
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

        GameObject target = ll.tileObjects[X, Y];
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
        if (ll != null && X >= 0 && X < ll.Width && Y >= 0 && Y < ll.Height && ll.tileObjects[X, Y] == gameObject)
        {
            ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            ll.tileObjects[X, Y] = null;
        }
        
        // Unregister from TurnManager
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }

        Destroy(gameObject);
    }
}