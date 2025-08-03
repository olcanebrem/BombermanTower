using UnityEngine;
using TMPro; // TextMeshPro kullanacağımız için bu gerekli

public class GameManager : MonoBehaviour
{
    // Singleton kurulumu
    public static GameManager Instance { get; private set; }

    // Inspector'dan atayacağımız UI metni
    public TextMeshProUGUI coinText;

    // Oyun durumu değişkenleri
    private int coinsCollected = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // İsteğe bağlı: Sahneler arası geçişte GameManager'ın yok olmamasını sağlar
            // DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Oyun başladığında UI'ı ilk değerle güncelle
        UpdateCoinUI();
    }

    /// <summary>
    /// Para toplandığında çağrılır. Skoru artırır ve UI'ı günceller.
    /// </summary>
    public void CollectCoin()
    {
        coinsCollected++;
        Debug.Log($"Coin toplandı! Toplam: {coinsCollected}");
        UpdateCoinUI();
    }

    /// <summary>
    /// Ekrondaki para metnini günceller.
    /// </summary>
    private void UpdateCoinUI()
    {
        if (coinText != null)
        {
            // Para sembolü için sprite etiketimizi kullanabiliriz!
            coinText.text = $"<sprite name=coin> {coinsCollected}";
        }
    }
}