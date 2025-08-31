using UnityEngine;

/// <summary>
/// CoinTile - Collectible tile that gets destroyed by LevelLoader when collected
/// NOTE: No Die() method needed - LevelLoader.DestroyTileAt() handles destruction
/// </summary>
public class CoinTile : TileBase, ICollectible, IInitializable
{
    public override TileType TileType => TileType.Coin;
    
    public void Init(int x, int y) { } // Position tracking handled by LevelLoader
    
    public bool OnCollect(GameObject collector)
    {
        Debug.Log($"[CoinTile] OnCollect called by: {collector?.name}");
        
        // PlayerController veya PlayerAgent tarafından toplanabilir
        bool isPlayer = collector.GetComponent<PlayerController>() != null;
        bool isPlayerAgent = collector.GetComponent<PlayerAgent>() != null;
        
        if (isPlayer || isPlayerAgent)
        {
            Debug.Log("[CoinTile] Collected by player/agent - will be destroyed");
            GameManager.Instance.CollectCoin();
            return true; // Evet, ben toplandım ve yok edilmeliyim.
        }
        
        Debug.Log("[CoinTile] Not collected by player/agent - staying");
        return false; // Başka bir şey toplarsa, yok edilme.
    }
    
    void OnDestroy()
    {
        Debug.Log($"[CoinTile] Being destroyed at position: {transform.position}");
    }
}