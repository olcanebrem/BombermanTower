using UnityEngine;

public class CoinTile : TileBase, ICollectible, IInitializable // IInitializable ekleyelim
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public void Init(int x, int y) { this.X = x; this.Y = y; }

    public void OnCollect(GameObject collector)
    {
        if (collector.GetComponent<PlayerController>() != null)
        {
            // 1. Etkiyi yarat (skoru artır).
            GameManager.Instance.CollectCoin();
            
            // 2. TÜM İZLERİ ANINDA TEMİZLE.
            var ll = LevelLoader.instance;
            ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty); // Mantıksal haritayı temizle.
            ll.tileObjects[X, Y] = null; // Nesne haritasını temizle.

            // 3. Görsel nesneyi yok etme listesine ekle.
            Destroy(gameObject);
        }
    }
}