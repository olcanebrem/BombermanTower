using UnityEngine;

public static class MovementHelper
{
    /// <summary>
    /// Bir IMovable nesnesini verilen yönde hareket ettirmeyi dener.
    /// Hareketin TÜM kurallarını (sınırlar, geçilebilirlik) içinde barındırır.
    /// </summary>
    /// <returns>Hareket başarılı olduysa true, olamadıysa false döndürür.</returns>
    public static bool TryMove(IMovable mover, Vector2Int direction)
{
    var ll = LevelLoader.instance;
    int newX = mover.X + direction.x;
    int newY = mover.Y + direction.y;

    // ... Sınır ve geçilebilirlik kontrolleri aynı ...
    if (newX < 0 || newX >= ll.width || newY < 0 || newY >= ll.height) return false;
    TileType targetType = TileSymbols.DataSymbolToType(ll.levelMap[newX, newY]);
    if (!IsTilePassable(targetType)) return false;

    // --- UYGULAMA (Tüm kontrollerden geçti) ---

    // 1. Görseli güncelle
    mover.gameObject.transform.position = new Vector3(
        newX * ll.tileSize,
        (ll.height - newY - 1) * ll.tileSize,
        0
    );

    // 2. Mantıksal haritayı (`levelMap`) güncelle
    ll.levelMap[mover.X, mover.Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
    ll.levelMap[newX, newY] = TileSymbols.TypeToDataSymbol(mover.TileType);

    // --- YENİ VE EN ÖNEMLİ KISIM: NESNE HARİTASINI (`tileObjects`) GÜNCELLE ---
    // a) Yeni pozisyona, hareket eden nesnenin GameObject referansını koy.
    ll.tileObjects[newX, newY] = mover.gameObject;
    // b) Eski pozisyonu temizle (artık orada bir nesne yok).
    ll.tileObjects[mover.X, mover.Y] = null;
    // --------------------------------------------------------------------

    // 3. Nesnenin kendi iç koordinatlarını güncelle
    mover.OnMoved(newX, newY);

    // Hata ayıklama için haritayı yazdır
    // ll.DebugPrintMap();

    return true;
}

    /// <summary>
    /// Bir tile tipinin üzerinden geçilebilir olup olmadığını belirleyen YARDIMCI metod.
    /// Bu 'private' olabilir, çünkü sadece TryMove tarafından kullanılıyor.
    /// </summary>
    private static bool IsTilePassable(TileType type)
    {
        switch (type)
        {
            // Geçilebilir tiplerin "beyaz listesi"
            case TileType.Empty:
            case TileType.Coin:
            case TileType.Health:
            case TileType.Stairs:
                return true;

            // Diğer her şey geçilemezdir.
            default:
                return false;
        }
    }
}