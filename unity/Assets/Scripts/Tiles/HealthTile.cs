using UnityEngine;
public class HealthTile : TileBehavior
{
    public override void OnPlayerEnter()
    {
        Debug.Log("Health alındı!");
        // PlayerController.instance.Heal(1);
        Destroy(gameObject);
    }
}
