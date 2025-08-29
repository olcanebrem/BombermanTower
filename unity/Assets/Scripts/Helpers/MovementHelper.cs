using UnityEngine;

public static class MovementHelper
{
    /// <summary>
    /// Bir hareketin mantıksal olarak mümkün olup olmadığını kontrol eder,
    /// gerekli etkileşimleri (saldırı, toplama) tetikler ve haritaları günceller.
    /// </summary>
    /// <returns>Hareket başarılı olduysa ve bir animasyon gerekiyorsa true döndürür.</returns>
    public static bool TryMove(IMovable mover, Vector2Int direction, out Vector3 targetWorldPos)
    {
        var ll = LevelLoader.instance;
        targetWorldPos = Vector3.zero;
        
        Debug.Log($"[MovementHelper] TryMove - {mover.GetType().Name} at ({mover.X}, {mover.Y}) trying to move {direction}");

        // --- 1. GÜVENLİK KİLİDİ: "Ben Kimim ve Neredeyim?" ---
        if (mover == null || mover.gameObject == null) 
        {
            Debug.Log("[MovementHelper] FAILED - Mover is null or gameObject is null");
            return false; // Ölü birim hareket edemez.
        }
        
        GameObject currentObjAtPosition = ll.tileObjects[mover.X, mover.Y];
        if (currentObjAtPosition != mover.gameObject)
        {
            // Eğer o pozisyonda başka bir aktif obje varsa, bu gerçek bir tutarsızlık
            if (currentObjAtPosition != null && currentObjAtPosition.activeInHierarchy)
            {
                Debug.LogError($"[MovementHelper] VERİ TUTARSIZLIĞI: {mover.GetType().Name} kendini ({mover.X},{mover.Y}) sanıyor ama nesne haritasında '{currentObjAtPosition.name}' var!", mover.gameObject);
                return false;
            }
            else
            {
                // Check if mover still exists before fixing map
                if (mover == null || mover.gameObject == null)
                {
                    Debug.LogWarning("[MovementHelper] Mover destroyed during movement, skipping");
                    return false;
                }
                
                // Pozisyon boş veya inactive obje var - bu normaldir (level loading, respawn vs.)
                // Haritayı düzelt ve devam et
                Debug.LogWarning($"[MovementHelper] Pozisyon tutarsızlığı düzeltiliyor: {mover.GetType().Name} at ({mover.X},{mover.Y}) - haritada: {currentObjAtPosition?.name ?? "NULL"}");
                ll.tileObjects[mover.X, mover.Y] = mover.gameObject;
                Debug.Log($"[MovementHelper] Position fix - Added to tileObjects[{mover.X},{mover.Y}]: {mover.gameObject.name}");
                ll.levelMap[mover.X, mover.Y] = TileSymbols.TypeToDataSymbol(mover.TileType);
            }
        }

        // --- 2. HEDEF HESAPLAMA VE SINIR KONTROLÜ ---
        int newX = mover.X + direction.x;
        int newY = mover.Y + direction.y;
        
        Debug.Log($"[MovementHelper] Target position: ({newX}, {newY}), Map bounds: {ll.Width}x{ll.Height}");

        if (newX < 0 || newX >= ll.Width || newY < 0 || newY >= ll.Height)
        {
            Debug.Log($"[MovementHelper] FAILED - Target out of bounds: ({newX}, {newY})");
            return false;
        }

        // --- 3. ETKİLEŞİM VE KARAR VERME ---
        GameObject targetObject = ll.tileObjects[newX, newY];
        TileType targetType = TileSymbols.DataSymbolToType(ll.levelMap[newX, newY]);
        
        Debug.Log($"[MovementHelper] Target tile: {targetType}, Object: {targetObject?.name ?? "NULL"}");

        // a) Hedefte bir BİRİM var mı?
        if (IsUnit(targetType))
        {
            // Evet, bu bir saldırı durumu.
            if (targetObject != null)
            {
                bool moverIsPlayer = mover is PlayerController;
                bool targetIsPlayer = targetType == TileType.Player;

                if (moverIsPlayer != targetIsPlayer) // Sadece düşmanlar ve oyuncu birbirine vurabilir.
                {
                    var damageable = targetObject.GetComponent<IDamageable>();
                    if (damageable != null && damageable.CurrentHealth > 0)
                    {
                        damageable.TakeDamage(1);
                        // Eğer hedef bu saldırıyla öldüyse, üzerine yürü.
                        if (damageable.CurrentHealth <= 0)
                        {
                            // Hedef öldü, hareket başarılı sayılır ve devam eder.
                        }
                        else
                        {
                            return false; // Hedef ölmedi, hareket etme.
                        }
                    }
                }
                else
                {
                    return false; // Dost ateşi yok, hareket etme.
                }
            }
        }
        // b) Eğer hedef bir birim değilse, GEÇİLEBİLİR Mİ?
        else if (!IsTilePassable(mover, targetType))
        {
            Debug.Log($"[MovementHelper] FAILED - Tile not passable: {targetType} for {mover.GetType().Name}");
            return false;
        }
        
        // --- 4. UYGULAMA (Tüm kontrollerden geçti, hareket başarılı) ---

        // a) Eğer hedefte toplanabilir bir şey varsa, topla.
        var collectible = targetObject?.GetComponent<ICollectible>();
        bool wasCollected = collectible?.OnCollect(mover.gameObject) ?? false;

        // b) Görsel ve mantıksal güncellemeleri yap.
        targetWorldPos = new Vector3(newX * ll.tileSize, (ll.Height - newY - 1) * ll.tileSize, 0);
        
        // Eğer bir şey toplandıysa, onu haritadan kaldır
        if (wasCollected && targetObject != null)
        {
            // Hedef nesneyi yok et
            Object.Destroy(targetObject);
            // Harita güncellemelerini yap
            ll.levelMap[newX, newY] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            ll.tileObjects[newX, newY] = null;
        }
        
        // Eğer hareket eden nesne hala haritadaysa, eski konumunu temizle
        if (ll.tileObjects[mover.X, mover.Y] == mover.gameObject)
        {
            ll.levelMap[mover.X, mover.Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
            ll.tileObjects[mover.X, mover.Y] = null;
        }

        ll.levelMap[newX, newY] = TileSymbols.TypeToDataSymbol(mover.TileType);
        ll.tileObjects[newX, newY] = mover.gameObject;
        Debug.Log($"[MovementHelper] Movement - Added to tileObjects[{newX},{newY}]: {mover.gameObject.name}");
        
        mover.OnMoved(newX, newY);
        
        return true;
    }

    /// <summary>
    /// Bir TileType'ın "birim" (Player, Enemy vb.) olup olmadığını belirler.
    /// </summary>
    private static bool IsUnit(TileType type)
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

    /// <summary>
    /// Bir tile tipinin, hareket eden belirli bir birim için geçilebilir olup olmadığını belirler.
    /// </summary>
    public static bool IsTilePassable(IMovable mover, TileType targetType)
    {
        switch (targetType)
        {
            case TileType.Empty:
            case TileType.Gate:
                return true;
            case TileType.Coin:
            case TileType.Health:
                // Player veya PlayerAgent collectible'ları toplayabilir
                return (mover is PlayerController) || (mover.gameObject?.GetComponent<PlayerAgent>() != null);
            case TileType.Breakable:
                return false; // Breakable tile'lar geçilemez
            case TileType.Wall:
                return false; // Duvarlar geçilemez
            default:
                return false;
        }
    }
}