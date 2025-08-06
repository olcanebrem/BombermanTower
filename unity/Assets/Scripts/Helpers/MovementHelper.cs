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

        // --- 3. ETKİLEŞİM KONTROLÜ (Çarpışma ve Toplama) ---
        if (targetObject != null)
        {
            // a) Hedef "toplanabilir" bir şey mi?
            targetObject.GetComponent<ICollectible>()?.OnCollect(mover.gameObject);

            // b) Hedef "saldırılabilir bir BİRİM" mi?
            if (IsUnit(targetType))
            {
                // --- YENİ "DOST ATEŞİ" KONTROLÜ ---
                // Sadece DÜŞMANLAR ve OYUNCU birbirine hasar verebilir.
                // Düşman düşmana, oyuncu oyuncuya (kendine) vuramaz.
                bool moverIsPlayer = mover is PlayerController;
                bool targetIsPlayer = targetType == TileType.Player;

                // Eğer her ikisi de oyuncu veya her ikisi de düşmansa, bu bir "dost" çarpışmasıdır.
                // Hasar verme, sadece hareket etmelerini engelle.
                if (moverIsPlayer == targetIsPlayer)
                {
                    return false; // Hareketi engelle, hasar verme.
                }
                // ------------------------------------

                // Eğer biri oyuncu, diğeri düşmansa, bu bir "düşman" çarpışmasıdır.
                var damageable = targetObject.GetComponent<IDamageable>();
                damageable?.TakeDamage(1);
                
                return false; // Çarpışma olduğu için hareket BAŞARISIZ olur.
            }
        }
        
        // --- 4. GEÇİLEBİLİLİK KONTROLÜ (DUVAR SORUNUNU ÇÖZEN KISIM) ---
        // Etkileşimlerden sonra hedefin son durumunu KONTROL ET.
        // Bu, bir coin toplandıktan sonra o karenin boşa çıkmasını sağlar.
        // En önemlisi, DUVAR gibi geçilemez nesneleri burada yakalar.
        targetType = TileSymbols.DataSymbolToType(ll.levelMap[newX, newY]);
        if (!IsTilePassable(targetType))
        {
            return false;
        }

        // --- 4. UYGULAMA (Tüm kontrollerden geçti) ---
        // a) Mantıksal haritaları güncelle
        ll.levelMap[mover.X, mover.Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        ll.levelMap[newX, newY] = TileSymbols.TypeToDataSymbol(mover.TileType);
        ll.tileObjects[newX, newY] = mover.gameObject;
        ll.tileObjects[mover.X, mover.Y] = null;
        
        // b) Nesnenin kendi iç koordinatlarını güncelle
        mover.OnMoved(newX, newY);
        
        // c) Animasyon için hedef dünya pozisyonunu hesapla ve dışarıya bildir
        targetWorldPos = new Vector3(newX * ll.tileSize, (ll.height - newY - 1) * ll.tileSize, 0);

        return true;
    }

    /// <summary>
    /// Bir tile tipinin üzerinden geçilebilir olup olmadığını belirleyen merkezi kural.
    /// </summary>
    public static bool IsTilePassable(TileType type)
    {
        switch (type)
        {
            case TileType.Empty:
            case TileType.Coin:     // Coin'in üzerine gelinebilir (toplamak için)
            case TileType.Health:   // Health'in üzerine gelinebilir (toplamak için)
            case TileType.Stairs:
                return true;
            default:
                return false;
        }
    }
    public static bool IsUnit(TileType type)
    {
        switch (type)
        {
            case TileType.Player:
            case TileType.Enemy:
            case TileType.EnemyShooter:
                return true; // Evet, bunlar birimdir.
            default:
                return false; // Hayır, diğerleri (Breakable, Bomb, Projectile vb.) birim değildir.
        }
    }
}