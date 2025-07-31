using UnityEngine;

public static class MovementHelper
{
    /// <summary>
    /// Bir IMovable nesnesini verilen yönde hareket ettirmeyi dener.
    /// Hareketin tüm mantığını ve görsel güncellemesini merkezileştirir.
    /// </summary>
    /// <param name="mover">Hareket etmeye çalışan nesne (IMovable arayüzünü uygulayan).</param>
    /// <param name="direction">Hareketin yönü ve büyüklüğü (örn: (1,0) sağa bir kare).</param>
    /// <returns>Hareket başarılı olduysa true, olamadıysa false döndürür.</returns>
    public static bool TryMove(IMovable mover, Vector2Int direction)
    {
        // --- 1. VERİ TOPLAMA ---
        // Gerekli tüm bilgilere en başta erişelim.
        var ll = LevelLoader.instance;
        int newX = mover.X + direction.x;
        int newY = mover.Y + direction.y;

        // --- 2. DOĞRULAMA (Guard Clauses) ---
        // Bu "Guard Clause" yapısı, kodun okunmasını kolaylaştırır.
        // Eğer bir koşul sağlanmıyorsa, en başta metottan çıkarız.

        // Harita sınırlarının dışına mı çıkıyor?
        if (newX < 0 || newX >= ll.width || newY < 0 || newY >= ll.height)
        {
            return false; 
        }

        // Hedefteki karede ne var?
        TileType targetType = TileSymbols.SymbolToType(ll.levelMap[newX, newY]);

        // Hedefteki kareye girilebilir mi?
        // Bu switch yapısı, gelecekte yeni kurallar eklemeyi kolaylaştırır.
        // Örneğin, "Coin"lerin üzerinden geçilebilsin ama "Enemy"lerin üzerinden geçilemesin.
        switch (targetType)
        {
            case TileType.Empty:
                // Boş kare, devam et.
                break;
            
            // GEÇİLEMEZ TİPLER:
            case TileType.Wall:
            case TileType.Breakable: // Şimdilik kırılabilirlerin de içinden geçilemesin.
            case TileType.Gate:
            case TileType.Enemy:
            case TileType.EnemyShooter:
                return false; 

            // ÜZERİNDEN GEÇİLEBİLİR TİPLER:
            // case TileType.Coin:
            // case TileType.Health:
            //     // Şimdilik bir şey yapma, ama ileride bu nesneleri toplama mantığı eklenebilir.
            //     break;
        }

        // --- 3. UYGULAMA (Tüm kontrollerden geçti) ---

        // a) GÖRSEL GÜNCELLEME: Nesnenin transform.position'ını güncelle.
        mover.gameObject.transform.position = new Vector3(
            newX * ll.tileSize,
            (ll.height - newY - 1) * ll.tileSize,
            0
        );

        // b) MANTIKSAL HARİTA GÜNCELLEME: levelMap dizisini güncelle.
        ll.levelMap[mover.X, mover.Y] = TileSymbols.TypeToSymbol(TileType.Empty); // Eski yeri boşalt.
        ll.levelMap[newX, newY] = TileSymbols.TypeToSymbol(mover.TileType);      // Yeni yeri doldur.

        // c) NESNENİN KENDİ DURUMUNU GÜNCELLEME: Nesneye yeni koordinatlarını bildir.
        mover.OnMoved(newX, newY);

        // Hareket başarıyla tamamlandı.
        return true;
    }
}