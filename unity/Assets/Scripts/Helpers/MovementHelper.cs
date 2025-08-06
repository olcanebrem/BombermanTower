using UnityEngine;

public static class MovementHelper
{
    /// <summary>
    /// Bir hareketin mantıksal olarak mümkün olup olmadığını kontrol eder.
    /// Etkileşimleri tetikler ve başarılı olursa mantıksal haritaları günceller.
    /// </summary>
    /// <param name="mover">Hareket etmeye çalışan nesne.</param>
    /// <param name="direction">Hareketin yönü.</param>
    /// <param name="targetWorldPos">Eğer hareket başarılı olursa, animasyonun hedefleyeceği dünya pozisyonu.</param>
    /// <returns>Hareketin mantıksal olarak yapılıp yapılamadığı.</returns>
    public static bool TryMove(IMovable mover, Vector2Int direction, out Vector3 targetWorldPos)
    {
        {
        var ll = LevelLoader.instance;
        targetWorldPos = Vector3.zero;

        // --- 1. GÜVENLİK KİLİDİ: "Ben Kimim ve Neredeyim?" ---
        // Birimin kendi bildiği pozisyon ile haritadaki pozisyonun tutarlı olduğundan emin ol.
        // Bu, "GPS Bozuldu" (veri tutarsızlığı) hatalarını en başından yakalar.
        if (ll.levelMap[mover.X, mover.Y] != TileSymbols.TypeToDataSymbol(mover.TileType))
        {
            Debug.LogError($"VERİ TUTARSIZLIĞI: {mover.TileType} kendini ({mover.X},{mover.Y}) sanıyor ama haritada orada değil! Hareket engellendi.", mover.gameObject);
            return false;
        }

        // --- 2. HEDEF HESAPLAMA VE SINIR KONTROLÜ ---
        int newX = mover.X + direction.x;
        int newY = mover.Y + direction.y;

        if (newX < 0 || newX >= ll.width || newY < 0 || newY >= ll.height)
        {
            return false; // Harita dışına çıkamazsın.
        }

        // --- 3. HEDEF ANALİZİ VE ETKİLEŞİM ---
        GameObject targetObject = ll.tileObjects[newX, newY];
        TileType targetType = TileSymbols.DataSymbolToType(ll.levelMap[newX, newY]);

        if (targetObject != null)
        {
            // a) Hedef "toplanabilir" bir şey mi?
            var collectible = targetObject.GetComponent<ICollectible>();
            if (collectible != null)
            {
                if (mover is PlayerController)
                {
                    collectible.OnCollect(mover.gameObject);
                }
                else { return false; } // Sadece oyuncu toplayabilir.
            }
            // b) Hedef "saldırılabilir bir BİRİM" mi?
            else if (IsUnit(targetType))
            {
                bool moverIsPlayer = mover is PlayerController;
                bool targetIsPlayer = targetType == TileType.Player;

                if (moverIsPlayer != targetIsPlayer) // Sadece farklı taraflar birbirine hasar verebilir.
                {
                    targetObject.GetComponent<IDamageable>()?.TakeDamage(1);
                }
                return false; // Çarpışma olduğu için hareket her zaman BAŞARISIZ olur.
            }
        }
        
        // --- 4. GEÇİLEBİLİRLİK KONTROLÜ ---
        // Etkileşimlerden sonra (örn: coin toplandıktan sonra) hedefin son durumunu kontrol et.
        targetType = TileSymbols.DataSymbolToType(ll.levelMap[newX, newY]);
        if (!IsTilePassable(targetType))
        {
            return false;
        }

        // --- 5. UYGULAMA (Tüm kontrollerden geçti) ---
        
        // a) Animasyon için hedef dünya pozisyonunu hesapla.
        targetWorldPos = new Vector3(newX * ll.tileSize, (ll.height - newY - 1) * ll.tileSize, 0);
        
        // b) "Akıllı Temizlik": Sadece kendi yerini temizle.
        if (ll.tileObjects[mover.X, mover.Y] == mover.gameObject)
        {
            ll.levelMap[mover.X, mover.Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            ll.tileObjects[mover.X, mover.Y] = null;
        }

        // c) Yeni pozisyonu, hareket eden birimin kimliğiyle güncelle.
        ll.levelMap[newX, newY] = TileSymbols.TypeToDataSymbol(mover.TileType);
        ll.tileObjects[newX, newY] = mover.gameObject;
        
        // d) Nesnenin kendi iç koordinatlarını güncelle ("GPS'i Tamir Et").
        mover.OnMoved(newX, newY);
        
        return true;
    }
    }
    // IsUnit metodu aynı kalır.
    public static bool IsUnit(TileType type)
    {
        switch (type)
        {
            case TileType.Player:
            case TileType.Enemy:
            case TileType.EnemyShooter:
                return true;
            default:
                return false;
        }
    }

    // IsTilePassable metodu, artık toplanabilirleri içermemeli.
    // Çünkü toplanma mantığı yukarıda özel olarak ele alınıyor.
    public static bool IsTilePassable(TileType type)
    {
        switch (type)
        {
            case TileType.Empty:
            case TileType.Stairs:
                return true;
            default:
                return false;
        }
    }
}