using UnityEngine;

public class PlaceBombAction : IGameAction
{
    private readonly PlayerController placer;
    private readonly Vector2Int targetDirection; // YENİ: Hedef yön

    public GameObject Actor => (placer as MonoBehaviour)?.gameObject;

    // Constructor artık bir yön de alıyor.
    public PlaceBombAction(PlayerController placer, Vector2Int targetDirection)
    {
        this.placer = placer;
        this.targetDirection = targetDirection;
    }

    public void Execute()
    {
        if (placer != null)
        {
            // PlaceBomb metoduna artık yönü de gönderiyoruz.
            placer.PlaceBomb(targetDirection);
        }
    }
}