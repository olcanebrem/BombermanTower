
using UnityEngine;
public class BombTile : TileBehavior
{
    public override void OnPlayerEnter()
    {
        Debug.Log("Bomb alındı!");
        Destroy(gameObject); // veya gizle
    }
}
