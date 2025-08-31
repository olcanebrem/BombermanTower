using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Debug = UnityEngine.Debug;

public class PlayerController : TileBase, IMovable, ITurnBased, IInitializable, IDamageable
{
    
    // ML-Agent reference removed - now handled centrally by TurnManager
    // --- Arayüzler ve Değişkenler ---
    public int X { get; private set; }
    public int Y { get; private set; }
    public override TileType TileType => TileType.Player;
    public bool HasActedThisTurn { get; set; }
    public GameObject bombPrefab;
    public GameObject explosionPrefab;
    private Vector2Int lastMoveDirection;
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public event Action OnHealthChanged;
    public static event Action<PlayerController> OnPlayerDeath;
    
    // Player death handled directly via PlayerAgent -> TurnManager
    private bool isAnimating = false;
    // --- YENİ ÇAPRAZ HAREKET TAMPONU DEĞİŞKENLERİ ---
    private Vector2Int bufferedMove; // Tamponlanan ilk hareket
    private float bufferTimer;       // Tamponun ne kadar süre geçerli olacağı
    public float diagonalBufferTime = 0.1f; // İkinci tuşa basmak için saniye cinsinden süre
    // -------------------------------------------------
    // --- Girdi Değişkenleri ---
    
    private Vector2Int moveIntent;
    private bool bombIntent;

    //=========================================================================
    // KAYIT VE KURULUM
    //=========================================================================
    
    private void Awake()
    {
        Debug.Log("[PlayerController] Player instance created");
    }
    
    public void ResetTurn() => HasActedThisTurn = false;
    void OnEnable() 
    { 
        if (TurnManager.Instance != null) 
        {
            TurnManager.Instance.Register(this); 
            Debug.Log("[PlayerController] Registered with TurnManager");
        }
        else
        {
            Debug.LogError("[PlayerController] TurnManager.Instance is null during OnEnable");
        }
    }
    void OnDisable()
    {
        // Eğer bu nesne, bir animasyonun ortasındayken yok edilirse...
        if (isAnimating)
        {
            // ...TurnManager'a animasyonun bittiğini bildir ki sayaç takılı kalmasın.
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.ReportAnimationEnd();
            }
        }
        
        // TurnManager'dan kaydı silme işlemi (bu zaten olmalı).
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }
    }
    
    public void Init(int x, int y)
    {
        this.X = x;
        this.Y = y;
        this.lastMoveDirection = Vector2Int.up;
        this.MaxHealth = 3;
        this.CurrentHealth = MaxHealth;
    }
    
    void Update()
    {
        // Debug Update calls
        if (Time.frameCount % 60 == 0) // Every second
        {
            Debug.Log($"[PlayerController] Update called - IsMLAgentActive: {(TurnManager.Instance?.IsMLAgentActive ?? false)}");
        }

        // Skip input if ML-Agent is controlling - handled by TurnManager
        if (TurnManager.Instance != null && TurnManager.Instance.IsMLAgentActive) 
        {
            return;
        }
          
        int horizontal = 0;
        int vertical = 0;
        if (Input.GetKey(KeyCode.W)) vertical = -1; // Up
        if (Input.GetKey(KeyCode.S)) vertical = 1;  // Down  
        if (Input.GetKey(KeyCode.A)) horizontal = -1; // Left
        if (Input.GetKey(KeyCode.D)) horizontal = 1;  // Right
        
        // Debug input
        if (horizontal != 0 || vertical != 0)
        {
            Debug.Log($"[PlayerController] Input detected - H:{horizontal}, V:{vertical}, Keys: W:{Input.GetKey(KeyCode.W)}, A:{Input.GetKey(KeyCode.A)}, S:{Input.GetKey(KeyCode.S)}, D:{Input.GetKey(KeyCode.D)}");
        }



        if (Input.GetKeyDown(KeyCode.Space)) bombIntent = true;
        
        // Only update moveIntent if there's actual input (don't reset to zero)
        if (horizontal != 0 || vertical != 0)
        {
            moveIntent = new Vector2Int(horizontal, vertical);
            Debug.Log($"[PlayerController] moveIntent set to: {moveIntent}");
        }
        // Don't reset moveIntent to zero here - let GetAction handle it
    }

    //=========================================================================
    // TUR TABANLI EYLEMLER (ITurnBased)
    //=========================================================================
    
    public IGameAction GetAction()
    {
        if (HasActedThisTurn) return null;

        // Don't act if ML-Agent is controlling this player
        if (TurnManager.Instance != null && TurnManager.Instance.IsMLAgentActive)
        {
            Debug.Log("[PlayerController] ML-Agent is active - PlayerController yielding control");
            return null;
        }
        
        Debug.Log($"[PlayerController] GetAction - moveIntent: {moveIntent}, bombIntent: {bombIntent}");

        // 3. Hareket niyetini kontrol et.
        if (moveIntent != Vector2Int.zero)
        {
            HasActedThisTurn = true;
            Vector2Int moveDirection = moveIntent;
            moveIntent = Vector2Int.zero; // Clear intent after using
            Debug.Log($"[PlayerController] Creating MoveAction with direction: {moveDirection}");
            return new MoveAction(this, moveDirection);
        }
        
        // 1. Bomba için en mantıklı yönü belirle (en son hareket yönü)
        Vector2Int bombDirection = lastMoveDirection;
        // 2. Bomba niyetini kontrol et.
        if (bombIntent)
        {
            HasActedThisTurn = true;
            bombIntent = false;
            
            // PlaceBombAction'ı, az önce belirlediğimiz en mantıklı yönle oluştur.
            return new PlaceBombAction(this, bombDirection);
        }
        
        
        return null;
    }
    
    // Animasyonu başlatmak için yeni bir public metod.
    public void StartMoveAnimation(Vector3 targetPos)
    {
        StartCoroutine(SmoothMove(targetPos));
    }

    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        // --- BAYRAKLARI AYARLA ---
        isAnimating = true;
        TurnManager.Instance.ReportAnimationStart();
        
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;
        float moveDuration = (TurnManager.Instance != null) ? (TurnManager.Instance.animationInterval > 0 ? TurnManager.Instance.animationInterval : TurnManager.Instance.turnInterval * 0.5f) : 0.1f;
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
        if (CurrentHealth < 0) CurrentHealth = 0;

        Debug.Log($"Oyuncu {damageAmount} hasar aldı! Kalan can: {CurrentHealth}");

        // --- YENİ HASAR EFEKTİ ---
        // Kendi "hasar aldım" animasyonunu başlat.
        StartCoroutine(FlashColor(Color.red));
        // -------------------------

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


    public void OnMoved(int newX, int newY)
    {
        // Hareket yönünü güncelle
        Vector2Int moveDelta = new Vector2Int(newX - X, newY - Y);
        if (moveDelta != Vector2Int.zero)
            lastMoveDirection = moveDelta;
        this.X = newX;
        this.Y = newY;
        Debug.Log($"Son hareket yönü: {lastMoveDirection} (Yeni pozisyon: {X},{Y})");
    }
        // Metod artık bir yön parametresi alıyor.
    public bool PlaceBomb(Vector2Int direction)
    {
        Debug.Log($"[PlayerController] PlaceBomb called with direction: {direction}");
        
        if (bombPrefab == null) 
        {
            Debug.Log("[PlayerController] bombPrefab is null!");
            return false;
        }

        var ll = LevelLoader.instance;
        if (ll == null)
        {
            Debug.Log("[PlayerController] LevelLoader.instance is null!");
            return false;
        }
        
        Debug.Log($"[PlayerController] Using LevelLoader from GameObject: {ll.gameObject.name}");

        // Sadece verilen direction yönüne bomba koymayı dene.
        int targetX = X + direction.x;
        int targetY = Y + direction.y;

        Debug.Log($"[PlayerController] Trying to place bomb at ({targetX}, {targetY}), player at ({X}, {Y})");

        if (targetX >= 0 && targetX < ll.Width && targetY >= 0 && targetY < ll.Height &&
            TileSymbols.DataSymbolToType(ll.levelMap[targetX, targetY]) == TileType.Empty)
        {
            Debug.Log("[PlayerController] Calling ll.PlaceBombAt()");
            ll.PlaceBombAt(targetX, targetY);
            return true;
        }

        Debug.Log($"Bomba koyulacak yer dolu veya geçersiz! TileType: {TileSymbols.DataSymbolToType(ll.levelMap[targetX, targetY])}");
        return false;
    }
    
    
    private IEnumerator FlashColor(Color flashColor)
    {
    // 1. Görseli sağlayan Image bileşenini al.
    Image visualImage = GetComponent<TileBase>()?.GetVisualImage();
    if (visualImage == null) yield break;

    // 2. Orijinal rengi sakla.
    Color originalColor = visualImage.color;

    // 3. Rengi doğrudan değiştir.
    visualImage.color = flashColor;

    // 4. Kısa bir süre bekle.
    yield return new WaitForSeconds(TurnManager.Instance.turnInterval * 0.8f);

    // 5. Rengi eski haline döndür.
    if (visualImage != null)
    {
        visualImage.color = originalColor;
    }
    }

    private void Die()
    {
        Debug.LogError("OYUNCU ÖLDÜ!");

          // Haritadan tek seferde sil
        if (LevelLoader.instance != null)
            LevelLoader.instance.ClearTile(X, Y);

        // Player disable
        gameObject.SetActive(false);
        Debug.Log("[PlayerController] Player disabled");

        // Event tetikle
        OnPlayerDeath?.Invoke(this);
    }
    
    
    // ML-Agent integration now handled centrally by TurnManager
    // All ML-Agent actions flow through ITurnBased/IGameAction system
}