using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public float turnInterval = 0.2f; // Hızı daha kontrol edilebilir yapmak için biraz artırdım.
    private float turnTimer = 0f;

    public int TurnCount { get; private set; } = 0;
    private List<ITurnBased> turnBasedObjects = new List<ITurnBased>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        turnTimer += Time.deltaTime;
        if (turnTimer >= turnInterval)
        {
            turnTimer -= turnInterval; // Tam olarak aralık kadar azaltmak, zaman kaymasını önler.
            AdvanceTurn();
        }
    }

    public void Register(ITurnBased obj)
    {
        if (!turnBasedObjects.Contains(obj)) turnBasedObjects.Add(obj);
    }

    public void Unregister(ITurnBased obj)
    {
        if (turnBasedObjects.Contains(obj)) turnBasedObjects.Remove(obj);
    }

    void AdvanceTurn()
    {
        TurnCount++;

        // 1. Herkesi yeni tura hazırla.
        foreach (var obj in turnBasedObjects.ToList())
        {
            obj.ResetTurn();
        }

        // 2. Oynayacakların bir kopyasını al ve KESİN BİR SIRAYA SOK.
        // Bu, tüm kaosun ve yarış durumlarının önüne geçen en önemli adımdır.
        var unitsToPlay = turnBasedObjects
            .OrderBy(u => GetExecutionOrder(u))
            .ToList();

        // 3. Sırayla herkesin "beynini" çalıştır.
        // Bu döngü anında, tek bir frame içinde, belirlenen sırada tamamlanır.
        foreach (var unit in unitsToPlay)
        {
            if (unit == null || (unit as MonoBehaviour) == null) continue;
            
            unit.ExecuteTurn();
        }
    }

    // Sıralamayı belirleyen merkezi kural motoru.
    private int GetExecutionOrder(ITurnBased unit)
    {
        if (unit is PlayerController) return 0; // Oyuncu her zaman ilk eylem şansına sahip olur.
        if (unit is EnemyShooterTile) return 1; // Düşmanlar ikinci.
        if (unit is EnemyTile) return 1;
        if (unit is Projectile) return 2;      // Mermiler en son hareket eder.
        if (unit is BombTile) return 3;        // Bombalar daha da sonra patlar.
        return 100; // Diğer her şey.
    }
}