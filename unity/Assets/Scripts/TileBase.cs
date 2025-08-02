using UnityEngine;
using TMPro; // TextMeshPro'yu kullanmak için bu satır ŞART!

public class TileBase : MonoBehaviour
{
    [SerializeField]
    // Değişkenin türünü TextMeshProUGUI olarak değiştiriyoruz.
    private TextMeshProUGUI visualText;

    void Awake()
    {
        // Eğer visualText Inspector'dan atanmamışsa, onu kendi bulsun.
        if (visualText == null)
        {
            visualText = GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    // Metod artık char değil, string alıyor.
    public void SetVisual(string symbol)
    {
        if (visualText != null)
        {
            visualText.text = symbol;
        }
    }
}