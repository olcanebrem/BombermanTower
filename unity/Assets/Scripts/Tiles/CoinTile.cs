using UnityEngine;
using System;
public class CoinTile : TileBase, ICollectible
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileType TileType => TileType.Coin;
    public bool HasActedThisTurn { get; set; }
    
    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
    public void OnCollect(GameObject collector)
    {
        // Eğer toplayan bir oyuncuysa...
        if (collector.GetComponent<PlayerController>() != null)
        {
            // GameManager'a haber ver.
            GameManager.Instance.CollectCoin();
            // Kendini yok et.
            Destroy(gameObject);
        }
    }
}
