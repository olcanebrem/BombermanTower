using UnityEngine;
using System;
using System.Collections;
using TMPro;
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
        // 1. Görseli sağlayan TextMeshPro bileşenini bul.
        var visualText = GetComponent<TileBase>()?.GetVisualText();
        if (visualText == null)
        {
            // Eğer TileBase veya TextMeshPro bulunamazsa, güvenlik için çık.
            yield break;
        }

        // 2. Metnin KENDİ CanvasRenderer'ını al.
        //    Bu, en spesifik ve en doğru hedeftir.
        CanvasRenderer renderer = visualText.canvasRenderer;
        if (renderer == null)
        {
            yield break;
        }

        // 3. O anki orijinal rengi sakla.
        Color originalColor = renderer.GetColor();

        // 4. Rengi, istediğimiz "flash" rengine ayarla.
        renderer.SetColor(flashColor);

        // 5. Kısa bir süre bekle.
        yield return new WaitForSeconds(TurnManager.Instance.turnInterval * 0.8f);

        // 6. Rengi, sakladığımız orijinal rengine geri döndür.
        if (renderer != null)
        {
            renderer.SetColor(originalColor);
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

        public void ExecuteTurn()
    {
        if (HasActedThisTurn) return;

        // ... bomba niyetini kontrol etme kısmı aynı ...
        if (bombIntent)
        {
            if (PlaceBomb())
            {
                HasActedThisTurn = true;
            }
            bombIntent = false;
            return;
        }

        // Hareket niyetini kontrol et.
        if (moveIntent != Vector2Int.zero)
        {
            if (MovementHelper.TryMove(this, moveIntent, out Vector3 targetPos))
            {
                // --- EN ÖNEMLİ DEĞİŞİKLİK ---
                // Artık hareketin çapraz olup olmadığını kontrol etmiyoruz.
                // Yapılan her başarılı hareket, yeni "son yön" olur.
                lastMoveDirection = moveIntent;
                // ---------------------------------
                
                StartCoroutine(SmoothMove(targetPos));
                HasActedThisTurn = true;
            }
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
        /// <summary>
    /// Oyuncunun komşu karelerine, en son baktığı yöne öncelik vererek bomba koymayı dener.
    /// </summary>
    /// <returns>Bomba başarıyla konulduysa true, konulamadıysa false döndürür.</returns>
    bool PlaceBomb()
    {
        if (bombPrefab == null) return false;

        var ll = LevelLoader.instance;

        // 1. Kontrol edilecek yönler için bir öncelik listesi oluştur.
        // Önce en son bakılan yönü, sonra diğerlerini dene.
        
        
            int targetX = X + lastMoveDirection.x;
            int targetY = Y + lastMoveDirection.y;

            // Hedefin harita içinde ve BOŞ olup olmadığını kontrol et.
            if (targetX >= 0 && targetX < ll.width && targetY >= 0 && targetY < ll.height &&
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