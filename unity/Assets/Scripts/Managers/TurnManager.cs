using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public float turnInterval = 0.01f; // Hızı daha kontrol edilebilir yapmak için biraz artırdım.
    private float turnTimer = 0f;

    public int TurnCount { get; private set; } = 0;
    private List<ITurnBased> turnBasedObjects = new List<ITurnBased>();
    // --- SENKRONİZASYON DEĞİŞKENLERİ ---
    private bool isTurnInProgress = false;
    private int activeAnimations = 0; // Aktif olan animasyonların sayısı
    // -----------------------------------------
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        // Eğer bir tur zaten devam ediyorsa, yenisini başlatma.
        if (isTurnInProgress) return;

        turnTimer += Time.deltaTime;
        if (turnTimer >= turnInterval)
        {
            turnTimer -= turnInterval;
            // AdvanceTurn'ü doğrudan çağırmak yerine, Coroutine'i başlat.
            StartCoroutine(AdvanceTurnCoroutine());
        }
    }
     // --- ANİMASYON KONTROL METODLARI ---
    public void ReportAnimationStart() => activeAnimations++;
    public void ReportAnimationEnd() => activeAnimations--;
    // ------------------------------------

    private IEnumerator AdvanceTurnCoroutine()
    {
        isTurnInProgress = true;
        TurnCount++;

        foreach (var obj in turnBasedObjects.ToList()) obj.ResetTurn();

        var unitsToPlay = turnBasedObjects.OrderBy(u => GetExecutionOrder(u)).ToList();

        // 1. MANTIKSAL TURU ANINDA ÇÖZ
        foreach (var unit in unitsToPlay)
        {
            if (unit != null && (unit as MonoBehaviour) != null)
            {
                unit.ExecuteTurn();
            }
        }

        // 2. GÖRSEL TURUN BİTMESİNİ BEKLE
        // Tüm animasyonlar bitene kadar (sayaç sıfır olana kadar) bu satırda bekle.
        yield return new WaitUntil(() => activeAnimations == 0);

        isTurnInProgress = false;
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
    if (unit is PlayerController) return 0;
    if (unit is EnemyShooterTile || unit is EnemyTile) return 1;
    if (unit is ExplosionWave) return 2; // YENİ SIRA
    if (unit is Projectile) return 3;
    if (unit is BombTile) return 4;
    return 100;
}
}