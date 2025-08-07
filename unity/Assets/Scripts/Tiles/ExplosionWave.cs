using UnityEngine;

public class ExplosionWave : TileBase, ITurnBased, IInitializable
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Explosion;
    public bool HasActedThisTurn { get; set; }
    
    private int stepsRemaining;
    private int deathTurn;
    private GameObject explosionPrefab;
    private Vector2Int direction;

    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() { if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this); }
    public void Init(int x, int y) { this.X = x; this.Y = y; }
    public void ResetTurn() => HasActedThisTurn = false;

    public IGameAction GetAction()
    {
        if (HasActedThisTurn) return null;
        HasActedThisTurn = true;
        
        if (TurnManager.Instance.TurnCount >= deathTurn)
        {
            Die();
            return null;
        }

        // Deal damage at current position
        DealDamageAt(X, Y);
        
        // Continue the wave if there are remaining steps
        if (stepsRemaining > 0)
        {
            int nextX = X + direction.x;
            int nextY = Y + direction.y;
            Spawn(explosionPrefab, nextX, nextY, direction, stepsRemaining - 1, deathTurn);
        }
        
        Die(); // Remove this wave after processing
        return null;
    }

    private static void DealDamageAt(int x, int y)
    {
        var ll = LevelLoader.instance;
        if (x < 0 || x >= ll.Width || y < 0 || y >= ll.Height) return;
        
        GameObject targetObject = ll.tileObjects[x, y];
        if (targetObject != null)
        {
            targetObject.GetComponent<IDamageable>()?.TakeDamage(1);
        }
    }

    public static void Spawn(GameObject prefab, int x, int y, Vector2Int dir, int range, int deathTurn)
    {
        if (range <= 0) return;

        var ll = LevelLoader.instance; 

        if (x < 0 || x >= ll.Width || y < 0 || y >= ll.Height || !MovementHelper.IsTilePassable(null, TileSymbols.DataSymbolToType(ll.levelMap[x, y])))
        {
            DealDamageAt(x, y);
            return;
        }

        Vector3 pos = new Vector3(x * ll.tileSize, (ll.Height - y - 1) * ll.tileSize, 0);
        GameObject waveGO = Instantiate(prefab, pos, Quaternion.identity, ll.transform);
        ExplosionWave wave = waveGO.GetComponent<ExplosionWave>();
        
        wave.Init(x, y);
        wave.direction = dir;
        wave.stepsRemaining = range;
        wave.deathTurn = deathTurn;
        wave.explosionPrefab = prefab;
        
        ll.levelMap[x, y] = TileSymbols.TypeToDataSymbol(wave.TileType);
        ll.tileObjects[x, y] = waveGO;
    }

    private void Die()
    {
        var ll = LevelLoader.instance;
        if (ll != null && X >= 0 && X < ll.Width && Y >= 0 && Y < ll.Height)
        {
            ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            ll.tileObjects[X, Y] = null;
        }
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }
}