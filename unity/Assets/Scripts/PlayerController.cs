using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Debug = UnityEngine.Debug;

public class PlayerController : TileBase, IMovable, ITurnBased, IInitializable, IDamageable
{
    // --- SINGLETON PATTERN ---
    public static PlayerController Instance { get; private set; }
    
    // --- ML-Agent ---
    public bool useMLAgent { get; set; }
    [Header("ML-Agent Support")]
    public PlayerAgent mlAgent;
    // --- Arayüzler ve Değişkenler ---
    public int X { get; private set; }
    public int Y { get; private set; }
    public TileType TileType => TileType.Player;
    public bool HasActedThisTurn { get; set; }
    public GameObject bombPrefab;
    public GameObject explosionPrefab;
    private Vector2Int lastMoveDirection;
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public event Action OnHealthChanged;
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
        // Singleton pattern implementation
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[PlayerController] Singleton instance created");
        }
        else if (Instance != this)
        {
            Debug.Log("[PlayerController] Duplicate instance destroyed");
            Destroy(gameObject);
            return;
        }
    }
    
    public void ResetTurn() => HasActedThisTurn = false;
    void OnEnable() { if (TurnManager.Instance != null) TurnManager.Instance.Register(this); }
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

        // Skip input if ML-Agent is controlling this player through turn-based system
        if (mlAgent != null && mlAgent.useMLAgent) return;
          
        int horizontal = 0;
        int vertical = 0;
        if (Input.GetKey(KeyCode.W)) vertical = -1;
        if (Input.GetKey(KeyCode.S)) vertical = 1;
        if (Input.GetKey(KeyCode.A)) horizontal = -1;
        if (Input.GetKey(KeyCode.D)) horizontal = 1;



        if (Input.GetKeyDown(KeyCode.Space)) bombIntent = true;
        moveIntent = new Vector2Int(horizontal, vertical);
    }

    //=========================================================================
    // TUR TABANLI EYLEMLER (ITurnBased)
    //=========================================================================
    
    public IGameAction GetAction()
    {
        if (HasActedThisTurn) return null;

        // 1. O anki en mantıklı yönü belirle:
        //    Eğer bir hareket tuşuna basılıyorsa, o yöndür.
        //    Değilse, en son hareket edilen yöndür.
        Vector2Int actionDirection = moveIntent != Vector2Int.zero ? moveIntent : lastMoveDirection;

        // 3. Hareket niyetini kontrol et.
        if (moveIntent != Vector2Int.zero)
        {
            HasActedThisTurn = true;
            return new MoveAction(this, moveIntent);
        }
        // 2. Bomba niyetini kontrol et.
        if (bombIntent)
        {
            HasActedThisTurn = true;
            bombIntent = false;
            
            // PlaceBombAction'ı, az önce belirlediğimiz en mantıklı yönle oluştur.
            return new PlaceBombAction(this, actionDirection);
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
        if (bombPrefab == null) return false;

        var ll = LevelLoader.instance;

        // Sadece verilen direction yönüne bomba koymayı dene.
        int targetX = X + direction.x;
        int targetY = Y + direction.y;

        if (targetX >= 0 && targetX < ll.Width && targetY >= 0 && targetY < ll.Height &&
            TileSymbols.DataSymbolToType(ll.levelMap[targetX, targetY]) == TileType.Empty)
        {
            ll.PlaceBombAt(targetX, targetY);
            return true;
        }

        Debug.Log("Bomba koyulacak yer dolu veya geçersiz!");
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
        // Oyuncuyu haritadan kaldır
        LevelLoader.instance.levelMap[X, Y] = TileSymbols.TypeToDataSymbol(TileType.Empty);
        LevelLoader.instance.tileObjects[X, Y] = null;
        // Oyunu durdur veya yeniden başlatma ekranını göster
        // Time.timeScale = 0; 
        Destroy(gameObject);
    }
    
    //=========================================================================
    // ML-AGENT INTERFACE METODLARI
    //=========================================================================
    
    // Note: ML-Agent integration now works through ITurnBased/IGameAction system
    // These methods are kept for backward compatibility but not used in new system
    /// <summary>
    /// [DEPRECATED] ML-Agent tarafından moveIntent ayarlamak için kullanılır - Use ITurnBased system instead
    /// </summary>
    public void SetMLMoveIntent(Vector2Int move) => moveIntent = move;
    
    /// <summary>
    /// [DEPRECATED] ML-Agent tarafından bombIntent ayarlamak için kullanılır - Use ITurnBased system instead
    /// </summary>
    public void SetMLBombIntent(bool bomb) => bombIntent = bomb;
    
    /// <summary>
    /// ML-Agent için mevcut moveIntent'i döndürür (debug için)
    /// </summary>
    public Vector2Int GetMoveIntent() => moveIntent;
    
    /// <summary>
    /// ML-Agent için mevcut bombIntent'i döndürür (debug için)
    /// </summary>
    public bool GetBombIntent() => bombIntent;
}