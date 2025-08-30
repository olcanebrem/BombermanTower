using UnityEngine;
using System.Collections;

public class BombTile : TileBase, ITurnBased, IInitializable, IMovable
{
    public int X { get; set; }
    public int Y { get; set; }
    public override TileType TileType => TileType.Bomb;
    public new GameObject gameObject => base.gameObject;
    public bool HasActedThisTurn { get; set; }
    private int turnCounter = 0;
    private int turnsToExplode = 3;
    public int explosionRange = 4;
    public GameObject explosionPrefab;          
    private Vector2Int lastFacingDirection;
    
    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() { if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this); }
    public void Init(int x, int y) { this.X = x; this.Y = y; }
    public void OnMoved(int newX, int newY) { this.X = newX; this.Y = newY; }
    public void StartMoveAnimation(Vector3 targetPosition) { /* Bombs don't animate movement */ }
    public void ResetTurn() => HasActedThisTurn = false;

    public IGameAction GetAction()
    {
        if (HasActedThisTurn) return null;
        
        HasActedThisTurn = true; // Düşman her tur bir şey yapmaya çalışır.

        turnCounter++;
        if (turnCounter >= turnsToExplode)
        {
            turnCounter = 0;
            Explode();
        }
        return null; // Pas geçti.
    }

    void Explode()
    {
        // Güvenlik için, menzilin pozitif olduğundan emin ol.
        if (explosionRange <= 0)
        {
            Debug.LogWarning($"Bomb at ({X},{Y}) has zero or negative range. Only exploding at center.");
            DealDamageAt(X, Y);
            Die();
            return;
        }

        Debug.Log($"Bomb at ({X},{Y}) exploded with range {explosionRange}!");

        // 1. "Ölüm Turunu" Hesapla
        int currentTurn = TurnManager.Instance.TurnCount;
        // Patlama, menzili kadar tur sürecek.
        int deathTurn = currentTurn + explosionRange;

        // Patlamanın merkezindeki nesneye hasar ver.
        DealDamageAt(X, Y);

        // 2. Dört ana yöne doğru İLK patlama dalgalarını ateşle.
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            int startX = X + dir.x;
            int startY = Y + dir.y;
            
            // --- EN ÖNEMLİ DÜZELTME ---
            // İlk dalgaya, kalan menzili ver.
            // Eğer toplam menzil 4 ise, ilk dalganın 3 adımı kalmıştır.
            ExplosionWave.Spawn(explosionPrefab, startX, startY, dir, explosionRange -1, deathTurn);
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
        if (x < 0 || x >= ll.Width || y < 0 || y >= ll.Height) return;

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