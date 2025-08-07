using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using Debug = UnityEngine.Debug;

public class PlayerController : TileBase, IMovable, ITurnBased, IInitializable, IDamageable
{
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
        void Update()
    {
        // --- GİRDİ OKUMA (Her Zaman Çalışır) ---
        // O anki klavye durumunu geçici değişkenlere oku.
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
    // TUR TABANLI EYLEMLER (ITurnBased) - Değişiklik Yok
    //=========================================================================
    public void ResetTurn() => HasActedThisTurn = false;

    public IGameAction GetAction()
    {
        if (HasActedThisTurn) return null;

        // Bomba niyetinin önceliği var.
        if (bombIntent)
        {
            HasActedThisTurn = true;
            bombIntent = false;
            // Yeni bir "Bomba Koy" eylemi oluştur ve döndür.
            return new PlaceBombAction(this);

        }
        
        if (moveIntent != Vector2Int.zero)
        {
            HasActedThisTurn = true;
            // Yeni bir "Hareket Et" eylemi oluştur ve döndür.
            return new MoveAction(this, moveIntent);
        }
        
        return null; // Bu tur yapacak bir eylem yok.
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

    public void OnMoved(int newX, int newY) { this.X = newX; this.Y = newY; }
    public bool PlaceBomb()
    {
        if (bombPrefab == null) return false;

        var ll = LevelLoader.instance;

        // 1. Kontrol edilecek yönler için bir öncelik listesi oluştur.
        // Önce en son bakılan yönü, sonra diğerlerini dene.
        
        
            int targetX = X + lastMoveDirection.x;
            int targetY = Y + lastMoveDirection.y;

            // Hedefin harita içinde ve BOŞ olup olmadığını kontrol et.
            if (targetX >= 0 && targetX < ll.Width && targetY >= 0 && targetY < ll.Height &&
                TileSymbols.DataSymbolToType(ll.levelMap[targetX, targetY]) == TileType.Empty)
            {
                // 3. İlk bulunan uygun yere bombayı koy ve işlemi bitir.
                ll.PlaceBombAt(targetX, targetY);
                return true; 
            }
        

        // 4. Eğer tüm komşu kareler doluysa, başarısızlık bildir.
        Debug.Log("Bomba koymak için etrafta boş yer yok!");
        return false;
    }
    
    

}