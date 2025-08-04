using UnityEngine;
using System;
using System.Collections;
public class PlayerController : TileBase, IMovable, ITurnBased, IInitializable, IDamageable
{
    // --- Arayüzler ve Değişkenler ---
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Player;
    public bool HasActedThisTurn { get; set; }
    public GameObject bombPrefab;

    private Vector2Int nextMoveDirection;
    private bool wantsToPlaceBomb;
    private Vector2Int lastMoveDirection; // Oyuncunun en son hareket ettiği yönü saklar.

    // --- Girdi Yönetimi ---
    void Update()
    {
        // Eğer bir sonraki tur için zaten bir komut ayarlanmışsa, yenisini alma.
        // Bu, oyuncunun çok hızlı komut vermesini engeller.
        if (nextMoveDirection != Vector2Int.zero || wantsToPlaceBomb) return;

        if (Input.GetKey(KeyCode.W)) nextMoveDirection = Vector2Int.down;
        else if (Input.GetKey(KeyCode.S)) nextMoveDirection = Vector2Int.up;
        else if (Input.GetKey(KeyCode.A)) nextMoveDirection = Vector2Int.left;
        else if (Input.GetKey(KeyCode.D)) nextMoveDirection = Vector2Int.right;
        else if (Input.GetKeyDown(KeyCode.Space)) wantsToPlaceBomb = true;
    }
    // --- IDamageable Arayüzünden Gelenler ---
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public event Action OnHealthChanged;

    public void TakeDamage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
        if (CurrentHealth < 0) CurrentHealth = 0;

        Debug.Log($"Oyuncu {damageAmount} hasar aldı! Kalan can: {CurrentHealth}");

        // Canımız değişti, herkese haber ver!
        OnHealthChanged?.Invoke();

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }
    public void Heal(int healAmount)
    {
        CurrentHealth += healAmount;

        // Canın, maksimum canı aşmasını engelle.
        if (CurrentHealth > MaxHealth)
        {
            CurrentHealth = MaxHealth;
        }

        Debug.Log($"Oyuncu {healAmount} can kazandı! Mevcut can: {CurrentHealth}");

        // Canımız değiştiği için, can barını güncelleyecek olan olayı tetikle.
        OnHealthChanged?.Invoke();
    }
    // --- ITurnBased Metodları ---
    public void ResetTurn() => HasActedThisTurn = false;

    private void Die()
    {
        Debug.LogError("OYUNCU ÖLDÜ!");
        // Oyuncuyu haritadan kaldır
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        LevelLoader.instance.tileObjects[X, Y] = null;
        // Oyunu durdur veya yeniden başlatma ekranını göster
        // Time.timeScale = 0; 
        Destroy(gameObject);
    }
    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;

        // Kaydedilmiş bir hareket niyeti varsa uygula
        if (nextMoveDirection != Vector2Int.zero)
        {
            if (MovementHelper.TryMove(this, nextMoveDirection, out Vector3 targetPos))
            {
                StartCoroutine(SmoothMove(targetPos));
                HasActedThisTurn = true;
            }
            // Niyet işleme konulduğu için sıfırla.
            nextMoveDirection = Vector2Int.zero;
        }
        // Kaydedilmiş bir bomba niyeti varsa uygula
        else if (wantsToPlaceBomb)
        {
            PlaceBomb();
            HasActedThisTurn = true;
            wantsToPlaceBomb = false;
        }
    }
    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        // Animasyonun başladığını TurnManager'a bildir.
        TurnManager.Instance.ReportAnimationStart();

        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;
        float moveDuration = 0.15f; // Animasyonun süresi (TurnManager'ın interval'ından küçük olmalı)

        while (elapsedTime < moveDuration)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / moveDuration);
            elapsedTime += Time.deltaTime;
            yield return null; // Bir sonraki frame'e kadar bekle
        }

        // Nesnenin tam olarak hedefte olduğundan emin ol.
        transform.position = targetPosition;

        // Animasyonun bittiğini TurnManager'a bildir.
        TurnManager.Instance.ReportAnimationEnd();
    }
    // --- Diğer Metodlar ---
    public void Init(int x, int y) { this.X = x; this.Y = y; this.MaxHealth = 3; this.CurrentHealth = MaxHealth; }
    public void OnMoved(int newX, int newY) { this.X = newX; this.Y = newY; }
    bool PlaceBomb()
    {
        if (bombPrefab == null) return false;

        // 1. Hedef koordinatları, "hafızadaki" yöne göre hesapla.
        int targetX = X + lastMoveDirection.x;
        int targetY = Y + lastMoveDirection.y;

        var ll = LevelLoader.instance;

        // 2. Hedefin harita içinde ve BOŞ olup olmadığını kontrol et.
        if (targetX >= 0 && targetX < ll.width && targetY >= 0 && targetY < ll.height &&
            TileSymbols.DataSymbolToType(ll.levelMap[targetX, targetY]) == TileType.Empty)
        {
            // 3. Eğer hedef uygunsa, bombayı koy ve başarı bildir.
            ll.PlaceBombAt(targetX, targetY);
            return true; 
        }
        else
        {
            // 4. Eğer hedef uygun değilse, başarısızlık bildir.
            Debug.Log("Bomba konulacak yer dolu veya harita dışında!");
            return false;
        }
    }
    
    // KAYIT METODLARI (ÇOK ÖNEMLİ)
    void OnEnable()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this);
    }

}