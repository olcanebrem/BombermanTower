using UnityEngine;

public class ExplosionWave : TileBase, ITurnBased, IInitializable
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Explosion;
    public bool HasActedThisTurn { get; set; }
    public static SpriteDatabase spriteDatabase;
    private Vector2Int direction;
    private int stepsRemaining;
    private int deathTurn; // Patlamanın tamamen yok olacağı tur numarası
    private GameObject explosionPrefab;

    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() { if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this); }

    public void Init(int x, int y) { this.X = x; this.Y = y; }
    public void OnMoved(int newX, int newY) { }

        public static void Spawn(GameObject prefab, int x, int y, Vector2Int dir, int range, int deathTurn)
    {
        if (range <= 0) return;

        // LevelLoader'ın o anki örneğine zaten sahibiz: 'll'
        var ll = LevelLoader.instance; 

        if (x < 0 || x >= ll.width || y < 0 || y >= ll.height || !MovementHelper.IsTilePassable(TileSymbols.DataSymbolToType(ll.levelMap[x, y])))
        {
            DealDamageAt(x, y);
            return;
        }

        Vector3 pos = new Vector3(x * ll.tileSize, (ll.height - y - 1) * ll.tileSize, 0);
        GameObject waveGO = Instantiate(prefab, pos, Quaternion.identity, ll.transform);
        ExplosionWave wave = waveGO.GetComponent<ExplosionWave>();
        
        wave.Init(x, y);
        wave.direction = dir;
        wave.stepsRemaining = range;
        wave.deathTurn = deathTurn;
        wave.explosionPrefab = prefab;
        
        ll.levelMap[x, y] = TileSymbols.TypeToDataSymbol(wave.TileType);
        ll.tileObjects[x, y] = waveGO;
        
        // Sprite'ı, 'll' referansı üzerinden, LevelLoader'ın veritabanından al.
        wave.SetVisual(ll.spriteDatabase.GetSprite(wave.TileType));
    }

    public void ResetTurn() => HasActedThisTurn = false;

    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;

        // 1. ÖLÜM KONTROLÜ: Zamanımız geldi mi?
        if (TurnManager.Instance.TurnCount >= this.deathTurn)
        {
            Die();
            return;
        }

        // 2. YAYILMA KONTROLÜ: Hâlâ yayılma hakkımız var mı?
        if (stepsRemaining > 0)
        {
            // a) Kendi karesindekine hasar ver.
            DealDamageAt(X, Y);

            // b) Bir sonraki dalgayı, menzili bir azaltarak ve AYNI ÖLÜM TURU ile oluştur.
            int nextX = X + direction.x;
            int nextY = Y + direction.y;
            Spawn(this.explosionPrefab, nextX, nextY, this.direction, this.stepsRemaining - 1, this.deathTurn);
        }
        
        stepsRemaining = 0;

        HasActedThisTurn = true;
    }

    private static void DealDamageAt(int x, int y)
    {
        var ll = LevelLoader.instance;
        if (x < 0 || x >= ll.width || y < 0 || y >= ll.height) return;
        
        GameObject targetObject = ll.tileObjects[x, y];
        if (targetObject != null)
        {
            targetObject.GetComponent<IDamageable>()?.TakeDamage(1);
        }
    }

    private void Die()
    {
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        LevelLoader.instance.tileObjects[X, Y] = null;
        Destroy(gameObject);
    }
}