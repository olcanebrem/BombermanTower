using UnityEngine;

public class CoinTile : TileBase, ICollectible, IInitializable
{
    public void Init(int x, int y) { } // Artık X,Y tutmasına gerek yok.
    
    public bool OnCollect(GameObject collector)
    {
        if (collector.GetComponent<PlayerController>() != null)
        {
            GameManager.Instance.CollectCoin();
            return true; // Evet, ben toplandım ve yok edilmeliyim.
        }
        return false; // Başka bir şey toplarsa, yok edilme.
    }
}