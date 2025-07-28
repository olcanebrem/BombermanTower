using UnityEngine;

public class EnemyShooterTile : TileBehavior
{
    public override void OnPlayerEnter()
    {
        Debug.Log("EnemyShooterTile: Player entered.");
        // Buraya düşmana özel davranış ekle
    }
}
