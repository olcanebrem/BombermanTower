using UnityEngine;
using System.Collections;

public class ExplosionWave : TileBase, ITurnBased, IInitializable
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Explosion;
    public bool HasActedThisTurn { get; set; }

    private Vector2Int direction;
    private int stepsRemaining;
    private GameObject explosionPrefab; // Kendi prefabını bilmesi için

    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() { if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this); }

    public void Init(int x, int y) { this.X = x; this.Y = y; }
    public void OnMoved(int newX, int newY) { }

    // Bu, bir ExplosionWave oluşturmanın TEK YETKİLİ yoludur.
    public static void Spawn(GameObject prefab, int x, int y, Vector2Int dir, int range)
    {
        var ll = LevelLoader.instance;
        // Hedefin harita içinde ve geçilebilir olduğundan emin ol.
        if (x < 0 || x >= ll.width || y < 0 || y >= ll.height || !MovementHelper.IsTilePassable(TileSymbols.DataSymbolToType(ll.levelMap[x, y])))
        {
            // Eğer hedef duvar gibiyse, o yöndeki patlamayı durdur.
            // Ama duvara (veya hedefe) hasar ver.
            DealDamageAt(x, y);
            return;
        }

        Vector3 pos = new Vector3(x * ll.tileSize, (ll.height - y - 1) * ll.tileSize, 0);
        GameObject waveGO = Instantiate(prefab, pos, Quaternion.identity, ll.transform);
        ExplosionWave wave = waveGO.GetComponent<ExplosionWave>();
        
        wave.Init(x, y);
        wave.direction = dir;
        wave.stepsRemaining = range;
        wave.explosionPrefab = prefab;
        
        ll.levelMap[x, y] = TileSymbols.TypeToDataSymbol(wave.TileType);
        ll.tileObjects[x, y] = waveGO;
        wave.SetVisual(TileSymbols.TypeToVisualSymbol(wave.TileType));
    }

    public void ResetTurn() => HasActedThisTurn = false;

    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;

        // 1. Önce kendi karesindekine hasar ver.
        DealDamageAt(X, Y);

        // 2. Eğer yayılma menzili varsa, bir sonraki dalgayı oluştur.
        if (stepsRemaining > 0)
        {
            int nextX = X + direction.x;
            int nextY = Y + direction.y;
            // Kendini kopyalamak için kendi Spawn metodunu çağırır.
            Spawn(this.explosionPrefab, nextX, nextY, this.direction, this.stepsRemaining - 1);
        }

        // 3. Görevini tamamladıktan sonra (hasar verdi ve yayılmayı tetikledi), kendini yok et.
        // Bir patlama dalgası sadece bir tur yaşar.
        Die();
        HasActedThisTurn = true;
    }

    // Bu metodun da static olması gerekir ki Spawn içinden çağırılabilsin.
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
    
    // Bu script hareket etmediği için SmoothMove'a ihtiyacı yok.
}