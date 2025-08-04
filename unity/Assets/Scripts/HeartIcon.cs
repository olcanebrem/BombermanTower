using UnityEngine;
using UnityEngine.UI;

public class HeartIcon : MonoBehaviour
{
    public Image heartImage;
    public Sprite fullHeartSprite;
    public Sprite emptyHeartSprite;

    /// <summary>
    /// Kalbin durumunu dolu veya boş olarak ayarlar.
    /// </summary>
    public void SetState(bool isFull)
    {
        if (heartImage != null)
        {
            heartImage.sprite = isFull ? fullHeartSprite : emptyHeartSprite;
        }
    }
}