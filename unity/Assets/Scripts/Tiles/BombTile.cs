using UnityEngine;
using System.Collections;

public class BombTile : TileBase, ITurnBased, IInitializable
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Bomb;
    public bool HasActedThisTurn { get; set; }

    public int explosionRange = 4;
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
        Debug.Log($"Bomb at ({X},{Y}) exploded with range {explosionRange}!");

        // Patlamanın merkezindeki nesneye hasar ver.
        DealDamageAt(X, Y);

        // Dört ana yöne doğru ExplosionWave'leri ateşle.
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            int startX = X + dir.x;
            int startY = Y + dir.y;
            
            // Patlama dalgasını oluşturma işini tamamen ExplosionWave'in kendisine bırak.
            ExplosionWave.Spawn(explosionPrefab, startX, startY, dir, explosionRange);
        }

        // Bombanın kendisini sistemden temizle.
        Die();
    }

    /// <summary>
    /// Belirtilen koordinattaki IDamageable bir nesneye hasar verir.
    /// </summary>
    public void DealDamageAt(int x, int y)
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
    public void Die()
    {
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        LevelLoader.instance.tileObjects[X, Y] = null;
        Destroy(gameObject);
    }
}