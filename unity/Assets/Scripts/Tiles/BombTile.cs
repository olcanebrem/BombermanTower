using UnityEngine;
using System.Collections;

public class BombTile : TileBase, ITurnBased, IInitializable
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Bomb;
    public bool HasActedThisTurn { get; set; }

    public int explosionRange = 2;
    public int turnsToExplode = 3;
    public GameObject explosionPrefab;          
    private bool exploded = false;
    private int turnsElapsed = 0;

    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() { if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this); }
    public void Init(int x, int y) { this.X = x; this.Y = y; }
    public void ResetTurn() => HasActedThisTurn = false;

    public void ExecuteTurn()
    {
        if (HasActedThisTurn || exploded) return;

        turnsElapsed++;
        if (turnsElapsed >= turnsToExplode)
        {
            Explode();
            exploded = true;
        }
        HasActedThisTurn = true;
    }

        void Explode()
    {
        Debug.Log($"Bomb at ({X},{Y}) exploded!");

        // --- YENİ: İLK HASAR VE ETKİLEŞİM BÖLÜMÜ ---
        // 1. Patlamanın merkezini ve dört komşusunu kontrol et.
        Vector2Int[] explosionArea = new Vector2Int[]
        {
            new Vector2Int(X, Y),       // Merkez
            new Vector2Int(X + 1, Y),   // Sağ
            new Vector2Int(X - 1, Y),   // Sol
            new Vector2Int(X, Y + 1),   // Aşağı (Mantıksal)
            new Vector2Int(X, Y - 1)    // Yukarı (Mantıksal)
        };

        foreach (var pos in explosionArea)
        {
            // Bu pozisyondaki nesneye hasar vermeyi dene.
            DealDamageAt(pos.x, pos.y);
        }
        // ------------------------------------------------

        // --- MEVCUT: PATLAMA DALGALARINI OLUŞTURMA BÖLÜMÜ ---
        // Dört ana yöne doğru ExplosionWave'leri ateşle.
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            int startX = X + dir.x;
            int startY = Y + dir.y;
            var ll = LevelLoader.instance;

            if (startX >= 0 && startX < ll.width && startY >= 0 && startY < ll.height &&
                MovementHelper.IsTilePassable(TileSymbols.DataSymbolToType(ll.levelMap[startX, startY])))
            {
                ExplosionWave.Spawn(explosionPrefab, startX, startY, dir, explosionRange - 1);
            }
        }
        // ----------------------------------------------------

        // Bombanın kendisini sistemden temizle.
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        LevelLoader.instance.tileObjects[X, Y] = null;
        Destroy(gameObject);
    }

    /// <summary>
    /// Belirtilen koordinattaki IDamageable bir nesneye hasar verir.
    /// </summary>
    private void DealDamageAt(int x, int y)
    {
        var ll = LevelLoader.instance;

        // Koordinatların geçerli olduğundan emin ol.
        if (x < 0 || x >= ll.width || y < 0 || y >= ll.height) return;

        // O koordinattaki GameObject'i bul.
        GameObject targetObject = ll.tileObjects[x, y];
        if (targetObject != null)
        {
            // Üzerinde IDamageable arayüzü var mı diye bak.
            var damageable = targetObject.GetComponent<IDamageable>();
            if (damageable != null)
            {
                // Varsa, hasar ver!
                damageable.TakeDamage(1); // Örnek olarak 1 hasar
            }
        }
    }
}