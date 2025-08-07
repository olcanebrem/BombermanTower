// Dosya Adı: PlaceBombAction.cs
using UnityEngine;

public class PlaceBombAction : IGameAction
{
    private readonly PlayerController placer; // Sadece oyuncu bomba koyabilir

    public GameObject Actor => placer.gameObject;

    public PlaceBombAction(PlayerController placer)
    {
        this.placer = placer;
    }

    public void Execute()
    {
        // Bu eylemin tek görevi, PlayerController'ın PlaceBomb metodunu çağırmaktır.
        // PlaceBomb metodu, en uygun boş kareyi bulma ve LevelLoader'ı tetikleme
        // mantığını zaten içeriyor.
        if (placer != null)
        {
            placer.PlaceBomb();
        }
    }
}