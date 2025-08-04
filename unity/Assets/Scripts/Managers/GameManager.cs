using UnityEngine;
using UnityEngine.UI; // Image kullanmak için bu gerekli olabilir
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // --- Coin UI ---
    public TextMeshProUGUI coinText;
    private int coinsCollected = 0;

    // --- YENİ VE BASİTLEŞTİRİLMİŞ CAN SİSTEMİ ---
    public GameObject heartPrefab; // Basit, sadece Image içeren Heart_Prefab'ı buraya atayacağız.
    public Transform healthBarContainer; // Hiyerarşi'deki HealthBarContainer'ı buraya atayacağız.
    
    private IDamageable playerHealth;
    // ---------------------------------------------

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        UpdateCoinUI();
        UpdateHealthBar();
    }

    /// <summary>
    /// Oyuncu oluşturulduğunda LevelLoader tarafından çağrılır.
    /// Can barını oyuncunun sağlık sistemine bağlar.
    /// </summary>
    public void RegisterPlayer(IDamageable player)
    {
        playerHealth = player;
        // Oyuncunun OnHealthChanged olayına abone ol. Canı her değiştiğinde UpdateHealthBar'ı çağır.
        playerHealth.OnHealthChanged += UpdateHealthBar;
        
        // Can barını ilk kez oluştur.
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (playerHealth == null || healthBarContainer == null || heartPrefab == null) return;

        // 1. Konteynerin içindeki tüm eski kalpleri yok et.
        foreach (Transform child in healthBarContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. Oyuncunun MEVCUT canı kadar yeni kalp oluştur.
        for (int i = 0; i < playerHealth.CurrentHealth; i++)
        {
            Instantiate(heartPrefab, healthBarContainer);
        }
    }

    public void CollectCoin()
    {
        coinsCollected++;
        UpdateCoinUI();
    }

    private void UpdateCoinUI()
    {
        if (coinText != null)
        {
            // Sprite ismini kontrol et, MyGame_SpriteAsset'te ne ise o olmalı.
            coinText.text = $"<sprite name=coin> {coinsCollected}";
        }
    }
}