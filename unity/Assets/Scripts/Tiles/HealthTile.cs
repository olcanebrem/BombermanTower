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
        // 1. Toplayan nesnenin üzerinde bir 'PlayerController' bileşeni var mı diye KONTROL ET.
        PlayerController player = collector.GetComponent<PlayerController>();

        // 2. EĞER VARSA (yani 'player' değişkeni null değilse)...
        if (player != null)
        {
            // a) O 'PlayerController' bileşeninin 'Heal' metodunu çağır.
            player.Heal(healAmount);
            
            // b) Kendini yok et.
            Destroy(gameObject);
        }
        
        // 3. EĞER YOKSA (yani toplayan bir düşman veya başka bir şeyse), HİÇBİR ŞEY YAPMA.
        //    Bu, düşmanların can potlarını "tüketmesini" engeller.
    }
}
