using UnityEngine;

public class CoinTile : TileBase, ICollectible, IInitializable
{
    public override TileType TileType => TileType.Coin;
    
    public void Init(int x, int y) { } // Artık X,Y tutmasına gerek yok.
    
    public bool OnCollect(GameObject collector)
    {
        Debug.Log($"[CoinTile] OnCollect called by: {collector?.name}");
        
        if (collector.GetComponent<PlayerController>() != null)
        {
            Debug.Log("[CoinTile] Collected by player - will be destroyed");
            GameManager.Instance.CollectCoin();
            return true; // Evet, ben toplandım ve yok edilmeliyim.
        }
        
        Debug.Log("[CoinTile] Not collected by player - staying");
        return false; // Başka bir şey toplarsa, yok edilme.
    }
    
    void OnDestroy()
    {
        Debug.Log($"[CoinTile] Being destroyed at position: {transform.position}");
    }
}