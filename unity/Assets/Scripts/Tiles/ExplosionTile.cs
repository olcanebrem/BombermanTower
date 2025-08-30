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

        // First damage immediately
        DealDamageAtPosition();

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
        
        // Deal damage each turn
        DealDamageAtPosition();
        
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