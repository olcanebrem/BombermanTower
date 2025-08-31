using UnityEngine;

/// <summary>
/// HealthTile - Collectible tile that gets destroyed by LevelLoader when collected
/// NOTE: No Die() method needed - LevelLoader.DestroyTileAt() handles destruction
/// </summary>
public class HealthTile : TileBase, ICollectible, IInitializable
{
    public override TileType TileType => TileType.Health;
    
    public void Init(int x, int y) { } // Position tracking handled by LevelLoader
    public int healAmount = 1;
    public bool OnCollect(GameObject collector)
    {
        // PlayerController'ı kontrol et
        PlayerController player = collector.GetComponent<PlayerController>();
        if (player != null)
        {
            player.Heal(healAmount);
            return true; // Evet, ben toplandım ve yok edilmeliyim.
        }
        
        // PlayerAgent'ı kontrol et - PlayerAgent PlayerController'a sahip olmalı
        PlayerAgent agent = collector.GetComponent<PlayerAgent>();
        if (agent != null)
        {
            PlayerController agentPlayer = agent.GetComponent<PlayerController>();
            if (agentPlayer != null)
            {
                agentPlayer.Heal(healAmount);
                return true; // Evet, ben toplandım ve yok edilmeliyim.
            }
        }
        
        return false; // Başka bir şey toplarsa, yok edilme.
    }
}