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
    private Vector2Int lastMoveDirection;
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public event Action OnHealthChanged;
    // --- YENİ ÇAPRAZ HAREKET TAMPONU DEĞİŞKENLERİ ---
    private Vector2Int bufferedMove; // Tamponlanan ilk hareket
    private float bufferTimer;       // Tamponun ne kadar süre geçerli olacağı
    public float diagonalBufferTime = 0.1f; // İkinci tuşa basmak için saniye cinsinden süre
    // -------------------------------------------------

    private Vector2Int nextMoveDirection;
    private bool wantsToPlaceBomb;

    //=========================================================================
    // KAYIT VE KURULUM
    //=========================================================================
    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
    void OnDisable() { if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this); }
    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
        this.lastMoveDirection = Vector2Int.up;
        this.MaxHealth = 3;
        this.CurrentHealth = MaxHealth;
    }

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
    //=========================================================================
    // GİRDİ YÖNETİMİ (UPDATE) - TAMAMEN YENİDEN YAZILDI
    //=========================================================================
    void Update()
    {
        // Eğer bir sonraki tur için zaten bir komut ayarlanmışsa, yeni girdi alma.
        if (nextMoveDirection != Vector2Int.zero || wantsToPlaceBomb) return;

        // Zamanlayıcıyı güncelle
        if (bufferTimer > 0)
        {
            bufferTimer -= Time.deltaTime;
        }
        else
        {
            // Eğer zamanlayıcı dolduysa ve tamponda bir hareket varsa,
            // bu, tek bir tuşa basıldığı anlamına gelir.
            if (bufferedMove != Vector2Int.zero)
            {
                nextMoveDirection = bufferedMove;
                bufferedMove = Vector2Int.zero;
            }
        }

        // Girdileri oku
        Vector2Int currentInput = Vector2Int.zero;
        if (Input.GetKeyDown(KeyCode.W)) currentInput += Vector2Int.down;
        if (Input.GetKeyDown(KeyCode.S)) currentInput += Vector2Int.up;
        if (Input.GetKeyDown(KeyCode.A)) currentInput += Vector2Int.left;
        if (Input.GetKeyDown(KeyCode.D)) currentInput += Vector2Int.right;
        if (Input.GetKeyDown(KeyCode.Space)) wantsToPlaceBomb = true;

        // Eğer bir yön girdisi varsa...
        if (currentInput != Vector2Int.zero)
        {
            // Eğer tampon boşsa, bu ilk basılan tuştur.
            if (bufferedMove == Vector2Int.zero)
            {
                bufferedMove = currentInput;
                bufferTimer = diagonalBufferTime;
            }
            // Eğer tampon doluysa, bu ikinci basılan tuştur.
            else
            {
                // İki yönü birleştir (örneğin, sol + yukarı = sol-yukarı).
                // Sadece dikey ve yatay eksenler farklıysa birleştir.
                if (bufferedMove.x == 0 && currentInput.y == 0 || bufferedMove.y == 0 && currentInput.x == 0)
                {
                    nextMoveDirection = bufferedMove + currentInput;
                    bufferedMove = Vector2Int.zero;
                    bufferTimer = 0;
                }
            }
        }
    }

    //=========================================================================
    // TUR TABANLI EYLEMLER (ITurnBased) - Değişiklik Yok
    //=========================================================================
    public void ResetTurn() => HasActedThisTurn = false;

    public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;

        if (nextMoveDirection != Vector2Int.zero)
        {
            if (MovementHelper.TryMove(this, nextMoveDirection, out Vector3 targetPos))
            {
                lastMoveDirection = nextMoveDirection;
                StartCoroutine(SmoothMove(targetPos));
                HasActedThisTurn = true;
            }
            nextMoveDirection = Vector2Int.zero;
        }
        else if (wantsToPlaceBomb)
        {
            if (PlaceBomb()) HasActedThisTurn = true;
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
    
    

}