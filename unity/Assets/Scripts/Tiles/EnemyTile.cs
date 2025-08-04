using UnityEngine;
using System.Collections;
using System;
public class EnemyTile : TileBase, IMovable, ITurnBased, IInitializable, IDamageable
{
    // --- Arayüzler ve Değişkenler ---
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Enemy;
    public bool HasActedThisTurn { get; set; }
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public event Action OnHealthChanged;
    private bool isAnimating = false;
    
    //=========================================================================
    // KAYIT VE KURULUM
    //=========================================================================
    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() 
    {
    if (isAnimating)
        {
            // ...TurnManager'a animasyonun bittiğini bildir ki sayaç takılı kalmasın.
            if (TurnManager.Instance != null) TurnManager.Instance.ReportAnimationEnd();
            TurnManager.Instance.Unregister(this);
        }
    }
    public void Init(int x, int y) { this.X = x; this.Y = y; this.MaxHealth = 1; this.CurrentHealth = MaxHealth; }

    //=========================================================================
    // TUR TABANLI EYLEMLER (ITurnBased)
    //=========================================================================
    public void ResetTurn() => HasActedThisTurn = false;

    /// <summary>
    /// TurnManager tarafından, bu düşmanın sırası geldiğinde çağrılır.
    /// </summary>
    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;

        // --- Basit AI Karar Verme Mantığı ---
        // %50 ihtimalle rastgele bir yöne hareket etmeyi dene.
        if (UnityEngine.Random.value > 0.5f)
        {
            // Rastgele bir yön seç (çapraz hareket dahil)
            Vector2Int moveDirection = new Vector2Int(UnityEngine.Random.Range(-1, 2), UnityEngine.Random.Range(-1, 2));

            // Eğer seçilen yön (0,0) ise (yani yerinde durma), bir şey yapma.
            if (moveDirection == Vector2Int.zero)
            {
                HasActedThisTurn = true; // Pas geçmek de bir eylemdir.
                return;
            }
            
            // Hareketi mantıksal olarak dene.
            if (MovementHelper.TryMove(this, moveDirection, out Vector3 targetPos))
            {
                // Başarılı olursa, görsel animasyonu başlat.
                StartCoroutine(SmoothMove(targetPos));
            }
        }
        
        // Düşman eylemini (hareket etme veya pas geçme) yaptı.
        HasActedThisTurn = true;
    }

    //=========================================================================
    // HAREKET (IMovable)
    //=========================================================================
    public void OnMoved(int newX, int newY) { this.X = newX; this.Y = newY; }

    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        isAnimating = true;
        TurnManager.Instance.ReportAnimationStart();
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;
        float moveDuration = 0.15f;

        while (elapsedTime < moveDuration)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / moveDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPosition;
        TurnManager.Instance.ReportAnimationEnd();
        isAnimating = false;
    }

    public void TakeDamage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
        OnHealthChanged?.Invoke();
        if (CurrentHealth <= 0) Die();
    }
    private void Die()
    {
        // Mantıksal haritadaki izini temizle.
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        // Nesne haritasındaki referansını temizle.
        LevelLoader.instance.tileObjects[X, Y] = null;
        // GameObject'i yok et.
        Destroy(gameObject);
    }
}