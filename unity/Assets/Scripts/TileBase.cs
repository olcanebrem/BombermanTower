using UnityEngine;
using UnityEngine.UI; // Image bileşeni için bu ŞART!

public class TileBase : MonoBehaviour
{
    /// <summary>
    /// Bu tile'ın tipini döndürür. Alt sınıflar tarafından override edilmelidir.
    /// </summary>
    public virtual TileType TileType => TileType.Unknown;
    // Artık TextMeshPro değil, bir Image bileşeni bekliyoruz.
    [SerializeField]
    private Image visualImage;

    void Awake()
    {
        // Eğer Inspector'dan atanmamışsa, kendi bulsun.
        if (visualImage == null)
        {
            visualImage = GetComponentInChildren<Image>();
        }
    }

    /// <summary>
    /// Bu tile'ın görselini, verilen Sprite ile değiştirir.
    /// </summary>
    public void SetVisual(Sprite sprite)
    {
        if (visualImage != null)
        {
            visualImage.sprite = sprite;
        }
    }

    /// <summary>
    /// Bu TileBase'in kontrol ettiği Image bileşenini dışarıya sunar.
    /// FlashColor gibi efektler için kullanılır.
    /// </summary>
    public Image GetVisualImage()
    {
        return visualImage;
    }
}