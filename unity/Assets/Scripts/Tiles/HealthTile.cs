using UnityEngine;
using System;
public class HealthTile : TileBase, ICollectible
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileType TileType => TileType.Health;
    public bool HasActedThisTurn { get; set; }
    
    public int healAmount = 1;

    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
    public void OnCollect(GameObject collector)
    {
        PlayerController player = collector.GetComponent<PlayerController>();
        if (player != null)
        {
            // 1. Etkiyi yarat (skoru artır).
            player.Heal(healAmount);
            
            // 2. TÜM İZLERİ ANINDA TEMİZLE.
            var ll = LevelLoader.instance;
            ll.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty); // Mantıksal haritayı temizle.
            ll.tileObjects[X, Y] = null; // Nesne haritasını temizle.

            // 3. Görsel nesneyi yok etme listesine ekle.
            Destroy(gameObject);
        }
    }
}
