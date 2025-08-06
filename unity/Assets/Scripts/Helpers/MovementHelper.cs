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
        var ll = LevelLoader.instance;
        int newX = mover.X + direction.x;
        int newY = mover.Y + direction.y;
        
        targetWorldPos = Vector3.zero;

        // --- 1. SINIR KONTROLÜ ---
        if (newX < 0 || newX >= ll.width || newY < 0 || newY >= ll.height)
        {
            return false;
        }

        // --- 2. HEDEF ANALİZİ ---
        GameObject targetObject = ll.tileObjects[newX, newY];
        TileType targetType = TileSymbols.DataSymbolToType(ll.levelMap[newX, newY]);

        // --- ETKİLEŞİM KURALI ---
        if (targetObject != null)
        {
            // a) Hedef "toplanabilir" bir şey mi?
            var collectible = targetObject.GetComponent<ICollectible>();
            if (collectible != null)
            {
                if (mover is PlayerController)
                {
                    // OnCollect'i çağır. O, temizliği kendi yapacak.
                    collectible.OnCollect(mover.gameObject);
                }
                else { return false; }
            }
            // b) Hedef "saldırılabilir bir BİRİM" mi?
            else if (IsUnit(targetType)) // 'else if' kullanarak öncelik sırası oluşturduk.
            {
                bool moverIsPlayer = mover is PlayerController;
                bool targetIsPlayer = targetType == TileType.Player;

                if (moverIsPlayer != targetIsPlayer)
                {
                    // --- EN ÖNEMLİ DÜZELTME BURADA ---
                    // Hedefteki nesnenin IDamageable bileşenini al ve TakeDamage'i çağır.
                    var damageable = targetObject.GetComponent<IDamageable>();
                    damageable?.TakeDamage(1);
                }
                // Saldırıdan sonra, HAREKET ETME.
                return false;
            }
        }
        
        // --- 4. GEÇİLEBİLİRLİK KONTROLÜ ---
        // Etkileşimlerden sonra (örn: coin toplandıktan sonra) hedefin son durumunu kontrol et.
        targetType = TileSymbols.DataSymbolToType(ll.levelMap[newX, newY]);
        if (!IsTilePassable(targetType))
        {
            return false;
        }

        // --- 5. UYGULAMA (Eğer hedef boşsa veya toplanabilir bir şeyse) ---
        targetWorldPos = new Vector3(newX * ll.tileSize, (ll.height - newY - 1) * ll.tileSize, 0);
        
        // --- CASUS LOGLARI BURADA ---
        TileType moverType = mover.TileType;
        char oldSymbolOnMap = ll.levelMap[mover.X, mover.Y];
        char newSymbolToWrite = TileSymbols.TypeToDataSymbol(moverType);

        Debug.Log($"--- HAREKET UYGULANIYOR ---");
        Debug.Log($"Hareket Eden: {mover.GetType().Name} | Tipi: {moverType} | Haritadaki Eski Sembolü: '{oldSymbolOnMap}'");
        Debug.Log($"Eski Pozisyon ({mover.X},{mover.Y}) temizleniyor.");
        
        ll.levelMap[mover.X, mover.Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        
        Debug.Log($"Yeni Pozisyon ({newX},{newY}) '{newSymbolToWrite}' sembolü ile işaretlenecek.");
        
        ll.levelMap[newX, newY] = newSymbolToWrite;
        
        ll.tileObjects[newX, newY] = mover.gameObject;
        ll.tileObjects[mover.X, mover.Y] = null;
        
        mover.OnMoved(newX, newY);
        
        // Hareketten hemen sonra haritayı yazdırarak karşılaştırma yapalım.
        ll.DebugPrintMap();
        Debug.Log($"--- HAREKET BİTTİ ---");
        // -------------------------
        
        return true;
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