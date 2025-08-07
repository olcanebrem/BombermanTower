using UnityEngine;
public class HealthTile : TileBase, ICollectible, IInitializable
{
    public void Init(int x, int y) { } // Artık X,Y tutmasına gerek yok.
    public int healAmount = 1;
    public bool OnCollect(GameObject collector)
    {
        PlayerController player = collector.GetComponent<PlayerController>();
        if (player != null)
        {
            player.Heal(healAmount);
            return true; // Evet, ben toplandım ve yok edilmeliyim.
        }
        return false; // Başka bir şey toplarsa, yok edilme.
    }
}